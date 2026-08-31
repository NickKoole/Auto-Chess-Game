---
description: Needs a state:approved ticket. Pushes what is left, readies the PR, waits for CI, squash-merges, and clears the worktree away. The dry run is the checkpoint, not a confirmation prompt.
argument-hint: <issue-number> [--dry-run] [--keep] [--timeout <minutes>]
allowed-tools: Bash(node automation/ship.mjs:*), Bash(gh issue list:*), Bash(gh pr view:*), Bash(gh pr checks:*), Bash(git worktree list:*), Bash(git status:*), Bash(git log:*)
---

Ship ticket `$ARGUMENTS` — the last step of the pipeline, after you have read
the PR and labelled it `state:approved` yourself.

All of the logic lives in `automation/ship.mjs`. Your job is to run it, to make
sure the human sees what is about to happen *before* the irreversible part — via
the dry run, not a prompt — and to say plainly what happened afterwards. Do not reimplement any of its steps
with your own `gh` or `git` calls — the point of the script is that shipping
works the same way every time, whether an agent or a person runs it.

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
machine does not, and a command that talked you into it would defeat its own
purpose.

The list is there to be read, never to be acted on. Do not pick from it, do not
ask which was meant with AskUserQuestion, and do not proceed when it holds
exactly one entry — that last case is the trap, because agreeing to the only
option on screen is how the wrong thing gets merged on the day the list is
longer. Shipping squash-merges a PR and deletes a worktree: the ticket it acts
on is named deliberately or not at all. `ship.mjs` takes the same line, exiting
1 with a usage message rather than prompting.

If a number was given, use it.

## 2. Show what would happen

```
node automation/ship.mjs <n> --dry-run
```

This changes nothing. It checks every precondition — the label, a clean worktree
on the right branch, an open mergeable PR — and prints what the real run would
do.

If it refuses, relay the reasons as they are written and stop. They are
actionable as printed: an unpushed commit wants `finish.mjs`, a conflict wants a
rebase, a missing label wants you to go and read the PR.

A missing `state:approved` is the same rule as in step 1: relay it, and do
**not** offer to add the label to get past it.

## 3. Ship it

Typing `/ship <n>` **is** the go-ahead. Do not follow a passing dry run with an
AskUserQuestion that re-asks whether to merge. The command exists to merge, the
number named the ticket deliberately, and step 2 has just printed exactly what
is about to happen while changing nothing — that is the checkpoint. A
confirmation on top of it is a prompt whose only available answer is the one
already given, and it trains the reader to click through the prompts that do
carry information.

So when step 2 passes, go straight to the real run, in the background
(`run_in_background: true`) because it waits for CI and can take minutes:

```
node automation/ship.mjs <n>
```

Pass `--keep` if it was in `$ARGUMENTS` — it merges but leaves the worktree on
disk, for when the checkout is still wanted for follow-up work. Do not offer it
unprompted; the default is to clean up.

**Ask only when there is a real question**, which is exactly what step 2 exists
to surface: a dry-run refusal you cannot relay as actionable, a worktree holding
uncommitted work, a PR whose contents do not look like the ticket you were
asked to ship. Those have answers only the human has. "Shall I proceed?" does
not — they already told you.

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
- The script fast-forwards this checkout onto the merge last, so the work is on
  disk here and not only on GitHub. Say it moved and by how many commits. If it
  reports that it could not — a local edit in the way, or the checkout parked on
  another branch — pass that on with the command it printed and leave the tree
  alone. The merge already happened; that message is a chore, not a failure, and
  running `git pull` or stashing on the human's behalf is how an edit goes
  missing.

## Cleaning up after an older merge

`ship.mjs` also finishes the job for a ticket that was merged by hand. When the
PR is already merged, the `state:approved` requirement does not apply — the merge
consumed that label, the ticket is `state:done`, and what is left is only litter:
a worktree, a stale branch and a state file. Running it then removes those and
nothing else, which is worth offering when you notice `git worktree list` holding
checkouts for tickets that landed long ago.
