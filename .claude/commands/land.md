---
description: Needs a state:approved ticket. Pushes what is left, readies the PR, waits for CI, squash-merges, and clears the worktree away. Asks before merging.
argument-hint: [issue-number] [--dry-run] [--keep] [--timeout <minutes>]
allowed-tools: Bash(node automation/land.mjs:*), Bash(gh issue list:*), Bash(gh pr view:*), Bash(gh pr checks:*), Bash(git worktree list:*), Bash(git status:*), Bash(git log:*)
---

Land ticket `$ARGUMENTS` — the last step of the pipeline, after you have read
the PR and labelled it `state:approved` yourself.

All of the logic lives in `automation/land.mjs`. Your job is to run it, to make
sure the human sees what is about to happen *before* the irreversible part, and
to say plainly what happened afterwards. Do not reimplement any of its steps
with your own `gh` or `git` calls — the point of the script is that landing
works the same way every time, whether an agent or a person runs it.

## 1. Resolve the ticket

If a number was given, use it. If not, list the candidates:

```
gh issue list --label state:approved --state open --json number,title --limit 20
```

and ask which one with AskUserQuestion. If that list is empty, say so and stop —
do **not** offer to label something approved to get around it. Approving is the
one thing in this pipeline a human does and the machine does not, and a command
that talked you into it would defeat its own purpose.

## 2. Show what would happen

```
node automation/land.mjs <n> --dry-run
```

This changes nothing. It checks every precondition — the label, a clean worktree
on the right branch, an open mergeable PR — and prints what the real run would
do.

If it refuses, relay the reasons as they are written and stop. They are
actionable as printed: an unpushed commit wants `finish.mjs`, a conflict wants a
rebase, a missing label wants you to go and read the PR.

## 3. Ask before landing

Never run the real thing without asking first. It squash-merges a PR and deletes
a worktree — the two least reversible things in the whole pipeline.

Use AskUserQuestion, and put the state in front of them: the PR number, how many
commits, whether CI has already passed, and whether the worktree will be removed.
Offer:

- **Land it** — `node automation/land.mjs <n>`, in the background
  (`run_in_background: true`). It waits for CI, so it can take several minutes.
- **Land but keep the worktree** — `--keep`, for when the checkout is still
  wanted for follow-up work.
- **Not yet** — nothing runs. The PR stays exactly as it is.

## 4. Report what happened

Read the background output as it goes rather than waiting for the exit.

- The moment CI resolves, say whether it was green and how many checks ran.
- On a merge, give the PR number and say that `state:done` arrives from the
  `pr-closed` workflow rather than from this command.
- If CI failed, the script stops **before** merging. Say which checks failed and
  link the PR. Nothing has been merged, nothing has been deleted, and re-running
  after a fix picks up where it left off.
- If the merge succeeded but the worktree could not be removed, say so and pass
  on the exact command the script printed. Do not run a forced removal yourself
  — the worktree holding something unexpected is a fact to look at.

## Cleaning up after an older merge

`land.mjs` also finishes the job for a ticket that was merged by hand. When the
PR is already merged, the `state:approved` requirement does not apply — the merge
consumed that label, the ticket is `state:done`, and what is left is only litter:
a worktree, a stale branch and a state file. Running it then removes those and
nothing else, which is worth offering when you notice `git worktree list` holding
checkouts for tickets that landed long ago.
