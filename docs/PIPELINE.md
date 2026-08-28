# The pipeline

Tickets live on GitHub. Agents run on your machine. **Nothing polls, and nothing
runs in the background** — you start every run yourself, and the reviewer is
spawned directly by the dev session as it exits.

You build **with** the dev agent, not by dispatching it: `dev.mjs` opens a Claude
session in your terminal and gets out of the way until you exit. Everything
around that session is automated — worktree, commit sweep, push, draft PR, and
the review that follows.

## The loop

```
  you file a ticket ──────────────► GitHub  (forms + readiness gate)
                                       │
              you pick one, state:ready │
                                       ▼
  $ node automation/dev.mjs 42     ┌─────────────┐
                                   │ dev session │  Claude in your terminal
                                   │  (worktree) │  you steer it, it asks you
                                   └──────┬──────┘
                                          │ commits locally; you exit
                                          │ runner pushes + opens DRAFT PR
                                          ▼
                                   ┌─────────────┐
                                   │  reviewer   │  spawned directly — this is
                                   │ (read-only) │  the whole "auto review"
                                   └──────┬──────┘
                                          │ posts findings to the PR
                                          ▼
                              ╔═══════════════════════╗
                              ║      state:needs-you  ║ ◄── you read the diff
                              ║  draft PR + findings  ║     and the findings
                              ╚═══════════╦═══════════╝
                                          │
                    ┌─────────────────────┴──────────────────┐
                    │                                        │
        comment what you want changed              happy with it
                    │                                        │
        $ node automation/dev.mjs 42                  mark ready, approve,
        (resumes the same session,                    and merge
         re-reviews automatically)                          │
                    │                                        ▼
                    └────────────► back to needs-you      state:done
```

## Why there's no watcher

Polling exists to answer *"is there work for me?"* — a question that only
matters when a process is already running, idle, and can't see where work comes
from. Starting the dev agent by hand removes the first condition, and that
removes the second: the only remaining hand-off is dev → review, and both are in
the same process tree. `dev.mjs` just spawns `review.mjs`.

So GitHub is the ticket store, the diff host, and the record. It never tells
your machine to do anything.

## Commands

```bash
node automation/dev.mjs           # list the ready pool, pick one
node automation/dev.mjs 42        # build ticket 42 (or continue it)
node automation/dev.mjs 42 --fresh      # discard the agent's memory, start cold
node automation/dev.mjs 42 --no-review  # stop after the PR

node automation/review.mjs 42     # re-review by hand
```

Re-running `dev.mjs 42` on a ticket that already has a PR is a **revision run**:
it collects every comment you have left since the last session, resumes that same
session so the agent still has its context, and puts you back in front of it.
Exiting pushes and re-reviews as before.

## Reading the result

The PR body opens with **Summary** then **Approach** — the shape the agent
settled on before writing code, and where implementing it changed its mind. Read
those two before the diff. A design you disagree with is much cheaper to catch
there than three hundred lines later, and the reviewer checks the diff against
that stated approach, so an unexplained mismatch comes back as a finding.

## The dev session

`dev.mjs` does the setup — worktree, branch, the ticket and any new comments
written to `.agent/task.md` — and then hands you a Claude session attached to
your terminal, in that worktree. You are in the loop for the whole build: it
states its approach before writing code, asks you when a guess would be
expensive to undo, and you steer it.

Exit when you are happy. The runner takes over again from there: it sweeps any
uncommitted work into a commit, pushes, opens the draft PR, and starts the
reviewer.

The PR body comes from `.agent/report.md`, which the agent writes before
finishing. Both files live in a gitignored `.agent/` directory inside the
worktree, so nothing about the plumbing reaches a commit. If the session ends
without a report — you quit early, or it forgot — the PR gets a placeholder and
the runner says so on the way out.

## Giving feedback

Just comment on the PR normally. There are no slash commands — nothing is
listening for them, and a plain comment is what you'd write anyway. The next
`dev.mjs` session picks up everything newer than the last one.

## States

One `state:*` label per issue, enforced by `ticket-guard.yml`.

| Label | Meaning |
| --- | --- |
| `state:backlog` | Captured, not groomed. |
| `state:ready` | Your shortlist when you sit down to work. |
| `state:building` | A dev session is open. |
| `state:reviewing` | A review run is in progress. |
| `state:needs-you` | Machine's done. Your turn. |
| `state:approved` | You approved. Ready to merge. |
| `state:done` | Merged. |
| `state:blocked` | Needs a decision or something external. Set automatically if a run crashes. |

Nothing on your machine watches these. They're for *you* — a filter for "what
should I start?" and "what's waiting on me?"

## What GitHub Actions does

Coordination only. It dispatches nothing.

- **`ticket-guard`** — one state label at a time, and refuses `state:ready` for
  a ticket with no checkable acceptance criteria. The highest-value automation
  here. The agent can ask you what you meant now, but a ticket too vague to have
  acceptance criteria is one you have not finished thinking about — and the
  session is a bad place to discover that.
- **`epic-split`** — label an epic `split` and each Breakdown checkbox becomes a
  real child ticket. Deterministic parsing, not a model inventing work.
- **`bootstrap-labels`** — reconciles labels from `.github/labels.yml`.
- **`pr-closed`** — settles the state label when a PR merges or is abandoned.
- **`ci`** — two independent jobs. **server**: `dotnet restore/build/test`,
  skipped until a `.csproj` exists under `server/`. **client**: typecheck, lint,
  Vitest, build, skipped until `client/package.json` exists. Each half can go
  green on its own while the project is still being scaffolded.

## Safety

The dev agent is denied `git push`, `git remote`, `gh`, branch switching, and
every form of `rm` and `git clean` (`automation/settings/dev.json`). It writes
code and commits; **only `dev.mjs`
publishes.** Watching the session is not what keeps that true, which is the
point: even with you at the keyboard, work can only land on a draft PR on the
ticket's own branch — never on `main`, and never on a remote you did not intend.

The reviewer is denied `Write`, `Edit`, and `gh`. A reviewer that could quietly
fix what it finds would produce a clean report hiding its own work, and its
approval would stop distinguishing "this was correct" from "this was made
correct". It also runs in a **fresh session**, so it has no memory of writing the
code and no investment in defending it.

Neither agent can approve a PR. GitHub review state stays yours, so an approval
always means a human approved.

## Worktrees

Each ticket builds in `../Idle-Auto-Chess-Game-work/issue-<n>/` on branch
`agent/<n>-<slug>`, so your main checkout stays free while a session is open.

```bash
git worktree list
git worktree remove ../Idle-Auto-Chess-Game-work/issue-42
git worktree prune
```

## Session state

`.agent-state/issue-<n>.json` (gitignored) holds the Claude session id, PR
number, branch, and when feedback was last swept. Deleting one, or passing
`--fresh`, makes the next session start cold against the same branch — useful
when an agent has talked itself into a bad approach.
