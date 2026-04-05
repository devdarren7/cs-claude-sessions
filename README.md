# cs — Claude Sessions Manager

A phone-friendly CLI for searching, tagging, and resuming [Claude Code](https://claude.ai/code) sessions from your terminal.

## Why

Claude Code doesn't have built-in session management. If you run dozens of sessions a day, finding and resuming the right one means scrolling through `claude --resume` output. `cs` fixes that with search, tagging, and a quick-pick menu.

## Install

```bash
# Copy the script to your PATH
curl -fsSL https://raw.githubusercontent.com/devdarren/cs-claude-sessions/main/cs -o ~/.local/bin/cs
chmod +x ~/.local/bin/cs

# Or clone and symlink
git clone https://github.com/devdarren/cs-claude-sessions.git
ln -s "$(pwd)/cs-claude-sessions/cs" ~/.local/bin/cs
```

### Requirements

- Bash 4+
- Python 3.6+
- [Claude Code CLI](https://claude.ai/code) installed

## Usage

```
cs              Interactive menu — lists recent sessions, pick a number to resume
cs l            Same as above
cs s <query>    Search sessions by keyword
cs s -p <proj>  Search within a specific project
cs r <N>        Resume session #N from last list
cs t <tags..>   Tag the most recent session
cs t <N> <tags> Tag session #N from last list
cs b            Show bookmarked (tagged) sessions
cs b <tag>      Filter bookmarks by tag
cs ut <N> <tag> Remove tag(s) from session #N
```

## Examples

```bash
# List recent sessions and pick one
cs

# Search for sessions about "skincare"
cs s skincare

# Search "api" only in the devdarren project
cs s -p devdarren api

# Tag the last session
cs t youtube content

# Tag session #3 from the last list
cs t 3 urgent

# Show all sessions tagged "youtube"
cs b youtube
```

## How it works

`cs` reads Claude Code's `~/.claude/history.jsonl` file, parses session metadata (timestamps, projects, first messages), and presents them in a compact, numbered list. Tags are stored separately in `~/.claude/session-tags.json`.

When you pick a session, it runs `claude --resume <session-id>` to drop you right back in.

## Screenshot

```
  1)     2m ago ·   3 msgs · "so now we have 100 devdarren blog posts now we nee"
  2)    18m ago ·   1 msgs · "the cs claude manager is broken"
  3)     1h ago ·   3 msgs · "how do i create a shortcut for it"
  4)    11h ago ·  29 msgs · "we made this pretext js without typescript"
  5) yesterday  ·   7 msgs · "is there a way to mark claude conversations"
Pick [1-15] or q:
```

## License

MIT
