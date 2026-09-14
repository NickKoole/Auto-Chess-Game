---
description: List the state:ready tickets — your shortlist for what to build next. Falls back to in-flight and backlog when the pool is empty. Read-only; runs in a pipeline-runner agent.
context: fork
agent: pipeline-runner
background: false
allowed-tools: Bash(gh issue list:*), Bash(rtk gh issue list:*)
---

Ready to build:

!`gh issue list --label state:ready --state open --limit 50`

Everything else open, for context:

!`gh issue list --state open --limit 50`

## What to do with that

Both blocks are tab-separated: number, state, title, labels, last updated. An
empty block means no issues matched, not an error.

Report the **ready** list as a short table — number, title, area, size. Drop any
row labelled `type:epic`: `claim.mjs` refuses to build them, so they are never
candidates however they happen to be labelled. If that leaves nothing, say so
plainly.

The ready pool here is deliberately a shortlist of one — tickets get promoted to
`state:ready` one at a time rather than a whole epic at once — so finding it
empty, or holding a single ticket, is the normal case and not a problem to flag.

When it *is* empty, use the second block to say something useful rather than
stopping at "nothing ready":

- Anything on `state:building`, `state:reviewing` or `state:needs-you` is work
  already in flight. Lead with it — it answers "what should I be doing?" better
  than an empty shortlist does. `state:needs-you` means the machine has finished
  and is waiting on you.
- Then list the lowest-numbered non-epic `state:backlog` tickets as the natural
  candidates to promote next.

Do not change any labels, and do not offer to. This command only reads.
Promoting a ticket to `state:ready` is a decision, and `ticket-guard` rejects
one whose acceptance criteria are not checkable — that gate is the point.

Close by offering `/build <n>` for whichever ticket is the obvious next move,
if there is one.
