# The Missing CLI for Claude Code: Meet `cs`

If you're using **Claude Code**, Anthropic's new research-preview CLI, you already know it's a game-changer. It's fast, it's agentic, and it lives where we do: in the terminal.

But if you're like me and use it dozens of times a day across multiple projects, you've likely hit a wall. Claude Code saves your history, but finding that specific refactoring session from three days ago—or resuming a complex debugging thread on your phone—can feel like finding a needle in a haystack.

To solve my own "session fatigue," I built **`cs`** (Claude Sessions). It's a lightweight CLI tool designed to turn Claude's flat history file into a searchable, taggable, and organized library.

---

## Why build a wrapper for a CLI?

Claude Code stores everything in `~/.claude/history.jsonl`. It's great for the machine, but not exactly human-friendly when you have 100+ entries. I needed three things:

1.  **Searchability:** Find sessions by keywords.
2.  **Organization:** Tag "Work," "SideProject," or "Refactor" so I can filter out the noise.
3.  **Resumability:** A quick, numbered list where I can just type `cs r 3` and get back to work.

---

## How it Works

`cs` is a hybrid of **Bash** and **Python**. It's designed to be fast and zero-config.

-   **The Parser:** A Python script reads the `history.jsonl` file provided by Claude Code.
-   **The Tag Engine:** Since I didn't want to mess with Claude's internal files, `cs` maintains a separate, lightweight JSON file for your custom tags and bookmarks.
-   **The UI:** Everything is optimized for the terminal. It uses a compact, numbered list format that is especially "phone-friendly" if you're SSHing into your dev box or using Termux.

---

## The Workflow

Here is how `cs` fits into a daily dev cycle:

### 1. The Quick Glance
Just run `cs` to see your most recent sessions. No more guessing which session ID belongs to which project.
```bash
cs
```

### 2. Finding Your Place
Need to find that session where you were working on the API?
```bash
cs s "api fix"
```

### 3. Staying Organized
Tag a session so you can find it later, or bookmark it for high-priority tasks.
```bash
cs t 1 "refactor"  # Adds the 'refactor' tag to session #1
cs b               # View all bookmarks
```

### 4. Seamless Resuming
Once you find the session number, resuming is a single command. `cs` handles the heavy lifting of passing the session ID back to Claude.
```bash
cs r 5
```

---

## Mobile-First (Sort of)

One of the unexpected benefits of `cs` is how it performs on mobile. When I'm away from my desk and need to check a session via a phone terminal, I don't want to type long UUIDs. The numbered list (`1`, `2`, `3`...) makes it incredibly easy to navigate and resume threads with just a few taps.

## Get Started

If you're a heavy Claude Code user, give `cs` a spin. It's open source and easy to install.

**Check out the repo here:** [https://github.com/devdarren7/cs-claude-sessions](https://github.com/devdarren7/cs-claude-sessions)

Happy coding (and session managing)!
