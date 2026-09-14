---
description: Needs a state:approved ticket. Pushes what is left, readies the PR, waits for CI, squash-merges, and clears the worktree away. The dry run is the checkpoint, not a confirmation prompt. Runs in a background pipeline-runner agent.
argument-hint: <issue-number> [--dry-run] [--keep] [--timeout <minutes>]
context: fork
agent: pipeline-runner
allowed-tools: Bash(node automation/ship.mjs:*), Bash(gh issue list:*), Bash(gh pr view:*), Bash(gh pr checks:*), Bash(rtk gh issue list:*), Bash(rtk gh pr view:*), Bash(rtk gh pr checks:*), Bash(git worktree list:*), Bash(git status:*), Bash(git log:*), Bash(rtk git worktree list:*), Bash(rtk git status:*), Bash(rtk git log:*), Monitor
---

Ship ticket `$ARGUMENTS` — the last step of the pipeline, after the human has
read the PR and labelled it `state:approved`.

All of the logic lives in `automation/ship.mjs`. Your job is to run it, to make
sure the dry run checks everything *before* the irreversible part, and to say
plainly what happened afterwards. Do not reimplement any of its steps with your
own `gh` or `git` calls — the point of the script is that shipping works the
same way every time, whether Claude, Codex or a person runs it.

## 1. Resolve the ticket

**A ticket number is required.** If `$ARGUMENTS` does not contain one, stop —
but name the candidates on the way out, so the retry is one keystroke rather
than a hunt:

```
gh issue list --label state:approved --state open --json number,title --limit 20
```

Then fail with that list in the message:

> `/ship` needs a ticket number. Approved right now: #4 (1.3 - Deterministic
> tick loop). Run `/ship 4`.

When nothing is approved, say that instead — `/ship` needs a ticket number, and
nothing is `state:approved` yet — and do **not** offer to add the label to get
around it. Approving is the one thing in this pipeline a human does and the
machine does not.

The list is there to be read, never to be acted on. Do not pick from it, and do
not proceed when it holds exactly one entry — that last case is the trap,
because agreeing to the only option on screen is how the wrong thing gets merged
on the day the list is longer. Shipping squash-merges a PR and deletes a
worktree: the ticket it acts on is named deliberately or not at all.

If a number was given, use it.

## 2. Dry run

```
node automation/ship.mjs <n> --dry-run
```

This changes nothing. It checks every precondition — the label, a clean worktree
on the right branch, an open mergeable PR — and prints what the real run would
do.

If it refuses, relay the reasons as they are written and stop. They are
actionable as printed: an unpushed commit wants `finish.mjs`, a conflict wants a
rebase, a missing label wants the human to go and read the PR. A missing
`state:approved` is the same rule as in step 1: relay it, and do **not** suggest
adding the label.

Also stop, and say why, if the dry run shows a worktree holding uncommitted work
or a PR whose contents do not look like the ticket named. Those are questions
only the human can answer, and you cannot ask them.

If `--dry-run` was in `$ARGUMENTS`, stop here and report what the dry run
printed.

## 3. Ship it

Typing `/ship <n>` **is** the go-ahead, and the passing dry run is the
checkpoint. Go straight to the real run. It waits for CI and can take longer
than the Bash tool's 10-minute limit, so start it with
`run_in_background: true` and follow its output with Monitor until it exits:

```
node automation/ship.mjs <n>
```

Pass `--keep` and `--timeout <minutes>` if they were in `$ARGUMENTS`. `--keep`
merges but leaves the worktree on disk. Do not add it unprompted.

## 4. Report what happened

Your final message is what the human reads. Cover, from the script's output:

- Whether CI was green, and how many checks ran.
- On a merge: the PR number, and that `state:done` arrives from the `pr-closed`
  workflow rather than from this command.
- If CI failed: the script stopped **before** merging. Say which checks failed
  and link the PR. Nothing was merged or deleted, and re-running after a fix
  picks up where it left off.
- If the merge succeeded but the worktree could not be removed, say so and pass
  on the exact command the script printed. Do not run a forced removal yourself
  — the worktree holding something unexpected is a fact to look at.
- The script fast-forwards the main checkout onto the merge last. Say it moved
  and by how many commits. If it could not — a local edit in the way, or the
  checkout on another branch — pass that on with the command it printed and
  leave the tree alone. The merge already happened; that is a chore, not a
  failure, and running `git pull` or stashing on the human's behalf is how an
  edit goes missing.

## Cleaning up after an older merge

`ship.mjs` also finishes the job for a ticket that was merged by hand. When the
PR is already merged, the `state:approved` requirement does not apply — the merge
consumed that label, and what is left is only a worktree, a stale branch and a
state file. Running it then removes those and nothing else. If you notice
`git worktree list` holding checkouts for tickets that landed long ago, mention
it in your report.
