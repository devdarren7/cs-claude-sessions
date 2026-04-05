#!/usr/bin/env bash
# cs - Claude Sessions manager
# Phone-friendly CLI for searching, tagging, and resuming Claude Code sessions

set -euo pipefail

HISTORY="$HOME/.claude/history.jsonl"
TAGS_FILE="$HOME/.claude/session-tags.json"
CACHE_FILE="$HOME/.claude/.cs-last-list"

# Ensure data files exist
[[ -f "$TAGS_FILE" ]] || echo '{}' > "$TAGS_FILE"
[[ -f "$CACHE_FILE" ]] || echo '[]' > "$CACHE_FILE"

# ── Helpers ──────────────────────────────────────────────────────

sessions_json() {
  # Parses history.jsonl into a JSON array of unique sessions
  # Optional args: --query <text> --project <text> --tags-only --tag <tag>
  local query="" project="" tags_only=false tag_filter=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --query) query="$2"; shift 2 ;;
      --project) project="$2"; shift 2 ;;
      --tags-only) tags_only=true; shift ;;
      --tag) tag_filter="$2"; shift 2 ;;
      *) shift ;;
    esac
  done

  python3 << PYEOF
import json, sys, time
from datetime import datetime

query = """$query""".strip().lower()
project_filter = """$project""".strip().lower()
tags_only = $( $tags_only && echo "True" || echo "False" )
tag_filter = """$tag_filter""".strip().lower()

# Load tags
try:
    with open("$TAGS_FILE") as f:
        tags = json.load(f)
except:
    tags = {}

# Parse sessions from history
sessions = {}
for line in open("$HISTORY"):
    line = line.strip()
    if not line:
        continue
    d = json.loads(line)
    sid = d.get("sessionId", "")
    ts = d.get("timestamp", 0)
    msg = d.get("display", "")
    proj = d.get("project", "")

    if sid not in sessions:
        sessions[sid] = {
            "id": sid,
            "project": proj,
            "first_msg": msg.replace("\n", " ")[:80],
            "start": ts,
            "end": ts,
            "count": 0,
            "messages": []
        }
    sessions[sid]["end"] = max(sessions[sid]["end"], ts)
    sessions[sid]["count"] += 1
    # Keep first 5 messages for search
    if len(sessions[sid]["messages"]) < 5:
        sessions[sid]["messages"].append(msg.lower()[:200])

# Filter
results = []
for sid, info in sessions.items():
    info["tags"] = tags.get(sid, [])

    # Tags-only filter
    if tags_only and not info["tags"]:
        continue

    # Tag filter
    if tag_filter and tag_filter not in [t.lower() for t in info["tags"]]:
        continue

    # Project filter
    if project_filter and project_filter not in info["project"].lower():
        continue

    # Query filter - search in first msg and all stored messages
    if query:
        searchable = info["first_msg"].lower() + " " + " ".join(info["messages"])
        searchable += " " + " ".join(info["tags"]).lower()
        if query not in searchable:
            continue

    results.append(info)

# Sort by most recent activity
results.sort(key=lambda x: x["end"], reverse=True)

# Output top 20
print(json.dumps(results[:20]))
PYEOF
}

do_list() {
  local limit="${1:-15}"
  sessions_json | format_and_display
  echo ""
  read -rp "Pick [1-${limit}] or q: " choice
  [[ "$choice" == "q" || -z "$choice" ]] && exit 0
  do_resume "$choice"
}

do_search() {
  local query="" project=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      -p|--project) project="$2"; shift 2 ;;
      *) query="$query $1"; shift ;;
    esac
  done
  query="${query## }"

  if [[ -z "$query" ]]; then
    echo "Usage: cs search <query> [-p project]"
    exit 1
  fi

  sessions_json --query "$query" --project "$project" | format_and_display
  echo ""
  read -rp "Pick # to resume or q: " choice
  [[ "$choice" == "q" || -z "$choice" ]] && exit 0
  do_resume "$choice"
}

do_resume() {
  local num="$1"

  # Look up session ID from cache
  local sid
  sid=$(python3 -c "
import json
cache = json.load(open('$CACHE_FILE'))
for item in cache:
    if item['index'] == $num:
        print(item['id'])
        break
")

  if [[ -z "$sid" ]]; then
    echo "Invalid selection: $num"
    exit 1
  fi

  echo "Resuming session $sid..."
  exec claude --resume "$sid"
}

do_tag() {
  local num="" tags=()

  # If first arg is a number, it's the session index
  if [[ "${1:-}" =~ ^[0-9]+$ ]]; then
    num="$1"
    shift
  fi

  tags=("$@")

  if [[ ${#tags[@]} -eq 0 ]]; then
    echo "Usage: cs tag [N] <tag1> [tag2] ..."
    exit 1
  fi

  # Get session ID
  local sid
  if [[ -n "$num" ]]; then
    sid=$(python3 -c "
import json
cache = json.load(open('$CACHE_FILE'))
for item in cache:
    if item['index'] == $num:
        print(item['id'])
        break
")
  else
    # Tag the most recent session (from history)
    sid=$(python3 -c "
import json
sessions = {}
for line in open('$HISTORY'):
    d = json.loads(line.strip())
    sid = d.get('sessionId','')
    ts = d.get('timestamp',0)
    if sid not in sessions or ts > sessions[sid]:
        sessions[sid] = ts
best = max(sessions, key=sessions.get)
print(best)
")
  fi

  if [[ -z "$sid" ]]; then
    echo "Could not find session"
    exit 1
  fi

  # Add tags
  local tags_json
  tags_json=$(printf '%s\n' "${tags[@]}" | python3 -c "import json,sys; print(json.dumps([l.strip() for l in sys.stdin]))")

  python3 << PYEOF
import json

new_tags = json.loads('$tags_json')

with open("$TAGS_FILE") as f:
    data = json.load(f)

existing = data.get("$sid", [])
for tag in new_tags:
    if tag not in existing:
        existing.append(tag)
data["$sid"] = existing

with open("$TAGS_FILE", "w") as f:
    json.dump(data, f, indent=2)

print(f"Tagged: [{' '.join(existing)}]")
PYEOF
}

do_untag() {
  local num="$1"
  shift
  local tags=("$@")

  if [[ -z "$num" || ${#tags[@]} -eq 0 ]]; then
    echo "Usage: cs untag <N> <tag1> [tag2] ..."
    exit 1
  fi

  local sid
  sid=$(python3 -c "
import json
cache = json.load(open('$CACHE_FILE'))
for item in cache:
    if item['index'] == $num:
        print(item['id'])
        break
")

  if [[ -z "$sid" ]]; then
    echo "Invalid selection: $num"
    exit 1
  fi

  local tags_json
  tags_json=$(printf '%s\n' "${tags[@]}" | python3 -c "import json,sys; print(json.dumps([l.strip() for l in sys.stdin]))")

  python3 << PYEOF
import json

with open("$TAGS_FILE") as f:
    data = json.load(f)

rm_tags = json.loads('$tags_json')

existing = data.get("$sid", [])
for tag in rm_tags:
    if tag in existing:
        existing.remove(tag)

if existing:
    data["$sid"] = existing
else:
    data.pop("$sid", None)

with open("$TAGS_FILE", "w") as f:
    json.dump(data, f, indent=2)

remaining = existing if existing else []
print(f"Tags: [{' '.join(remaining)}]" if remaining else "All tags removed.")
PYEOF
}

do_bookmarks() {
  local tag_filter="${1:-}"

  if [[ -n "$tag_filter" ]]; then
    sessions_json --tags-only --tag "$tag_filter" | format_and_display
  else
    sessions_json --tags-only | format_and_display
  fi
  echo ""
  read -rp "Pick # to resume or q: " choice
  [[ "$choice" == "q" || -z "$choice" ]] && exit 0
  do_resume "$choice"
}

show_help() {
  cat << 'EOF'
cs - Claude Sessions manager

COMMANDS:
  cs              Interactive menu (recent sessions)
  cs l            Same as above
  cs s <query>    Search sessions by keyword
  cs s -p <proj>  Search within a project
  cs r <N>        Resume session #N from last list
  cs t <tags..>   Tag the most recent session
  cs t <N> <tags> Tag session #N from last list
  cs b            Show bookmarked (tagged) sessions
  cs b <tag>      Filter bookmarks by tag
  cs ut <N> <tag> Remove tag(s) from session #N

EXAMPLES:
  cs s skincare          Search for "skincare"
  cs s -p devdarren api  Search "api" in devdarren project
  cs t youtube content   Tag last session with "youtube" and "content"
  cs t 3 urgent          Tag session #3 from last list
  cs b youtube           Show all sessions tagged "youtube"
EOF
}

format_and_display() {
  local input
  input=$(cat)
  CS_INPUT="$input" python3 << PYEOF
import json, sys, time, os
from datetime import datetime

data = json.loads(os.environ["CS_INPUT"])
if not data:
    print("  No sessions found.")
    sys.exit(0)

now = time.time() * 1000

cache = [{"index": i+1, "id": s["id"]} for i, s in enumerate(data)]
with open("$CACHE_FILE", "w") as f:
    json.dump(cache, f)

for i, s in enumerate(data):
    num = f"{i+1:>2}"

    tags_str = ""
    if s.get("tags"):
        tags_str = "[" + " ".join(s["tags"]) + "] "

    diff_ms = now - s["end"]
    diff_min = diff_ms / 60000
    if diff_min < 60:
        ago = f"{int(diff_min)}m ago"
    elif diff_min < 1440:
        ago = f"{int(diff_min/60)}h ago"
    elif diff_min < 2880:
        ago = "yesterday"
    elif diff_min < 10080:
        ago = f"{int(diff_min/1440)}d ago"
    else:
        ago = datetime.fromtimestamp(s["end"]/1000).strftime("%b %d")

    proj = s["project"].replace("/home/darren", "~")
    if proj == "~":
        proj = ""
    else:
        proj = f" {proj}"

    msg = s["first_msg"][:50].strip()
    if not msg:
        msg = "(empty)"

    tags_colored = f"\033[33m{tags_str}\033[0m" if tags_str else ""
    print(f" \033[36m{num})\033[0m {tags_colored}\033[90m{ago:>10}\033[0m \033[90m·\033[0m {s['count']:>3} msgs \033[90m·\033[0m \"{msg}\"\033[90m{proj}\033[0m")
PYEOF
}

# ── Main dispatch ────────────────────────────────────────────────

cmd="${1:-}"
shift 2>/dev/null || true

case "$cmd" in
  ""| "l" | "list")
    do_list
    ;;
  "s" | "search")
    do_search "$@"
    ;;
  "r" | "resume")
    do_resume "${1:?Usage: cs r <N>}"
    ;;
  "t" | "tag")
    do_tag "$@"
    ;;
  "ut" | "untag")
    do_untag "$@"
    ;;
  "b" | "bookmarks")
    do_bookmarks "$@"
    ;;
  "h" | "help" | "-h" | "--help")
    show_help
    ;;
  *)
    echo "Unknown command: $cmd"
    show_help
    exit 1
    ;;
esac
