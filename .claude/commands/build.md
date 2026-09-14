---
description: Needs a state:ready ticket (not an epic). Claims a worktree, has a ticket-builder agent plan and build it while you make the decisions here, then asks before the draft PR and the Claude review.
argument-hint: [issue-number, or blank to pick from state:ready] [--fresh] [--no-review]
allowed-tools: Agent, SendMessage, ToolSearch, AskUserQuestion, Read, Glob, Grep, Bash(node automation/claim.mjs:*), Bash(node automation/finish.mjs:*), Bash(node automation/review.mjs:*), Bash(gh issue list:*), Bash(gh pr view:*), Bash(rtk gh issue list:*), Bash(rtk gh pr view:*), Bash(git -C:*), Bash(rtk git -C:*)
---

Build ticket `$ARGUMENTS` end to end.

You are the **coordinator**, not the builder. A `ticket-builder` agent
(`.claude/agents/ticket-builder.md`, pinned to Sonnet) does the planning, code,
tests, commits and report inside the ticket worktree. You own everything that
involves the human: choosing the ticket, relaying the agent's questions,
publishing, and summarising the review. Agents can't ask the human anything
themselves, so every decision goes through you.

This is the `claim.mjs` → session → `finish.mjs` path from
[docs/PIPELINE.md](../../docs/PIPELINE.md). Do not run `automation/dev.mjs` or
`automation/session.mjs`: both open an interactive Codex inside this terminal.
Don't edit the worktree yourself. If something needs changing, send it to the
agent.

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
branch, writes `.agent/task.md`, and installs the push guard. Note the
`worktree:` and `branch:` lines. Ignore its "Start the agent" instructions:
they launch Codex.

If it refuses (an epic, or nothing to claim), relay the reason and stop.

## 3. Plan — spawn the builder

Spawn the agent with the Agent tool: `subagent_type: "ticket-builder"`,
`name: "build-<n>"`, `description: "Plan ticket #<n>"`. Its prompt:

```
PLAN phase for ticket #<n>.
Main checkout: <absolute path of this repo>
Worktree: <worktree path from claim>
Branch: <branch> (from <base>)
```

It runs in the background. Tell the human it is reading the ticket and code,
and wait for the completion notification. Do not poll it.

## 4. Checkpoint — the human decides

Show the agent's **Approach** and verification plan in a few lines. This is the
cheapest moment to redirect the build, so don't bury it.

- For each item under **Questions**, ask with AskUserQuestion, putting the
  agent's recommended option first. Batch up to four questions per call.
- If there are no questions, still ask one: go ahead with this approach, or
  change something. Use "Other" for redirections.

Then resume the **same** agent with SendMessage (load its schema first with
`ToolSearch` `select:SendMessage` if it isn't loaded):

```
BUILD phase. Decisions:
- <question> → <answer, verbatim where the human wrote free text>
```

Resuming keeps the code the agent has already read. Don't spawn a fresh one,
which would re-read everything.

## 5. Build loop

Wait for the notification. The agent returns either:

- **`## Blocked on a decision`**: ask the human with AskUserQuestion, then
  SendMessage the answer back with `BUILD phase, continue. Decision: …`. Repeat
  as needed.
- **`## Done`**: check its claims before passing them on. Read
  `<worktree>/.agent/report.md`, and run `git -C <worktree> status --short` and
  `git -C <worktree> log --oneline <base>..HEAD`. If the report is missing, the
  tree is dirty, or there are no commits, send the agent back to fix that before
  moving on.

Summarise the result in a few lines: what was built, the tests and their real
results, and anything incomplete or risky.

## 6. Ask about the review

Now — and only now — ask with AskUserQuestion:

- **Publish PR and Claude review** — run
  `node automation/finish.mjs <n> --reviewer claude` **in the background**
  (`run_in_background: true`). That one call is the whole hand-off: sweep, push,
  draft PR, runner validation, then a fresh headless Claude reviewer
  (read-only, `automation/settings/claude-review.json`). Don't split it into
  `--no-review` plus `review.mjs` to get the URL sooner. `--no-review` sets
  `state:needs-you` while the review would still be running.

  It runs for minutes. Report the `PR: <url>` line as soon as it appears in the
  output, then go to step 7 when it exits.
- **Publish PR only** — `node automation/finish.mjs <n> --no-review --reviewer claude`.
  Report the PR URL and say the review can be run later with
  `node automation/review.mjs <n>`; the reviewer choice is recorded.
- **Not yet** — publish nothing. The commits are safe on the branch in the
  worktree, and `/build <n>` or `node automation/finish.mjs <n> --reviewer claude`
  picks it up later.

If `--no-review` was in `$ARGUMENTS`, skip the question and take the
**Publish PR only** path.

Never run `finish.mjs` without asking first. It pushes the branch and opens a
draft PR, the one irreversible step in this command.

## 7. Bring the review back here, in plain words

Only if a review ran. Read the PR number from `.agent-state/issue-<n>.json`, then:

```
gh pr view <pr> --json comments --jq '[.comments[] | select(.body | contains("<!-- agent:review -->"))] | last | .body'
```

Select on the `<!-- agent:review -->` marker rather than taking the last
comment. A human comment landing after the review would otherwise be summarised
as if it were the review.

Then write a **short summary in plain language**, not the raw markdown:

- **Lead with the verdict in one sentence**: approved or not, how many findings,
  how many actually block a merge.
- **Say what was checked**: the runner built and tested server and client
  independently before the reviewer read the diff, and whether that passed. A
  failed validation forces a `changes` verdict, so say when that is the reason.
- **Put each finding in ordinary words**, worst first: what is wrong, which
  file, why it matters. Write "worth fixing before merge" or "take it or leave
  it" instead of severity labels.
- **Say which findings you think are right** and where the reviewer is wrong.
  If you're unsure, ask the builder with SendMessage before deciding. It wrote
  the code.
- **End with two or three concrete choices**. One of them is usually: send the
  findings worth fixing to the builder (SendMessage `REVISE phase` with the
  findings), then publish again with step 6.

Keep it to roughly a screen, and link the PR. State plainly, every time:

- **It is not an approval.** The reviewer is read-only with no `gh`, so GitHub
  review state and `state:approved` stay the human's (`/approve <n>`).
- **The PR is a draft**, and the ticket has moved to `state:needs-you`.

If the reviewer failed or posted no machine-readable verdict, say that instead
of inventing a summary. The code is fine; only the review didn't run, and
`node automation/review.mjs <n>` retries it.
