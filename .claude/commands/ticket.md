---
description: Build a GitHub ticket in its own worktree, then offer to run the reviewer
argument-hint: [issue-number] [--fresh] [--no-review]
allowed-tools: Read, Write, Edit, Glob, Grep, TodoWrite, Bash(node automation/claim.mjs:*), Bash(node automation/finish.mjs:*), Bash(node automation/review.mjs:*), Bash(gh issue list:*), Bash(dotnet build:*), Bash(dotnet restore:*), Bash(dotnet test:*), Bash(dotnet format:*), Bash(dotnet ef migrations:*), Bash(dotnet tool restore:*), Bash(npm run:*), Bash(npm ci), Bash(git status:*), Bash(git diff:*), Bash(git log:*), Bash(git show:*), Bash(git add:*), Bash(git commit:*), Bash(git rev-parse:*)
---

Build ticket `$ARGUMENTS` end to end. You are the dev agent for this run — the
human who typed this is sitting with you for the whole thing.

This is the `claim.mjs` → session → `finish.mjs` path from
[automation/README.md](../../automation/README.md), with *this* session as the
one that owns the build. Do not run `automation/dev.mjs`: it would try to open a
second Claude attached to the terminal you are already running inside.

## 1. Resolve the ticket

If a number was given, use it. If not, **do not** run `claim.mjs` with no
argument — it blocks on a stdin prompt this session cannot answer. Instead run:

```
gh issue list --label state:ready --json number,title,labels --limit 20
```

drop anything labelled `type:epic`, and ask which one with AskUserQuestion.
If the ready pool is empty, say so and stop.

## 2. Claim it

```
node automation/claim.mjs <n> [--fresh]
```

Pass `--fresh` only if it was in `$ARGUMENTS`. This creates the worktree and
branch, writes `.agent/task.md`, and installs the push guard. Read the
`worktree:` line it prints — that directory is where you work for the rest of
the run. Every file path, every `git` command, and every test command from here
on is relative to it, **not** to the main checkout.

If it refuses (an epic, or nothing to claim), relay the reason and stop.

## 3. Load your standing rules

Read `automation/prompts/dev-system.md` and follow it for this whole session.
It is the dev agent's system prompt and the source of truth for scope, when to
ask, the plan-first method, and the report format — do not work from a summary
of it. Then read `<worktree>/.agent/task.md`, which holds the ticket and any
feedback since the last run.

Note what the permission layer is *not* doing here: this session was not
launched with `--settings dev.json`, so the deny list that normally binds the
dev agent is absent. The worktree's `pre-push` hook still holds, so nothing can
reach the remote except `finish.mjs`. Treat the rest of that deny list as
binding anyway — no `gh`, no `git push`, no branch switching, no `rm`, no new
NuGet or npm packages.

## 4. Build it

State your approach before writing code, satisfy every acceptance criterion,
write tests that would fail if the logic were wrong, and actually run them.
Commit locally in the worktree as you go. Ask the human whenever a guess would
be expensive to undo — that is the entire reason this runs attached.

## 5. Write the report

Before you finish, write `<worktree>/.agent/report.md` using the exact structure
in `dev-system.md`. It becomes the PR body and the reviewer checks the diff
against it, so a missing or inaccurate report degrades the review that follows.

## 6. Ask about the review

Now — and only now — ask with AskUserQuestion:

- **Publish PR and review** — `node automation/finish.mjs <n>` **in the
  background** (`run_in_background: true`). That one call is the whole hand-off:
  sweep, push, draft PR, then the reviewer, in a single process. Do **not**
  split it into `--no-review` plus a separate `review.mjs` call to get the URL
  sooner — `finish.mjs` sets `state:needs-you` on its `--no-review` path, so the
  ticket would surface in the "waiting on me" filter while the review is still
  running. Keeping it whole gives the same label sequence as `dev.mjs`:
  `building → reviewing → needs-you`.

  It runs for minutes, so read the background output as it goes rather than
  waiting for the exit: report the `PR: <url>` line the moment it appears, then
  relay the reviewer's verdict and finding count when that arrives.
- **Publish PR only** — `node automation/finish.mjs <n> --no-review`. Report the
  PR URL and say the reviewer can be run later with
  `node automation/review.mjs <n>`.
- **Not yet** — publish nothing. The commits are safe on the branch in the
  worktree; say that `node automation/finish.mjs <n>` publishes when they are
  ready.

If `--no-review` was in `$ARGUMENTS`, skip the question and take the
**Publish PR only** path.

Never run `finish.mjs` without asking first: it pushes the branch and opens a
draft PR, and that is the one irreversible step in this command.
