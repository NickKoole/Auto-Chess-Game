---
description: Set state:approved on a ticket from here instead of the GitHub web UI. Shows the PR and the reviewer's findings first — that display is the checkpoint, not a confirmation prompt. Runs in a pipeline-runner agent.
argument-hint: <issue-number> [--dry-run]
context: fork
agent: pipeline-runner
background: false
allowed-tools: Bash(node automation/approve.mjs:*), Bash(gh issue list:*), Bash(rtk gh issue list:*)
---

Approve ticket `$ARGUMENTS`.

All of the logic lives in `automation/approve.mjs`. This is a convenience over
`gh issue edit --add-label state:approved` — it exists so the human doesn't
have to switch to the browser, not so the machine can talk itself into a label
it isn't allowed to set on its own. See `docs/PIPELINE.md` and `ship.mjs` for
why no other script in this pipeline writes `state:approved`: it is the one
point where a human says yes, and this command only runs when they type it.

## 1. Resolve the ticket

**A ticket number is required.** If `$ARGUMENTS` does not contain one, stop and
list the candidates:

```
gh issue list --label state:needs-you --state open --json number,title --limit 20
```

> `/approve` needs a ticket number. Waiting on you right now: #7 (1.6 - Battle
> event log for replay). Run `/approve 7`.

Do not pick from that list yourself, and do not proceed when it holds exactly
one entry — same reasoning as `/ship`: the ticket is named deliberately or not
at all.

## 2. Show what's there

```
node automation/approve.mjs <n> --dry-run
```

This prints the PR link and the reviewer's last findings comment, and changes
nothing. If there's no PR yet, or no reviewer comment, relay that and stop —
there's nothing to approve.

## 3. Approve it

If `--dry-run` was in `$ARGUMENTS`, stop after step 2.

Otherwise, typing `/approve <n>` **is** the go-ahead, same as `/ship`. Nothing
to re-confirm — run:

```
node automation/approve.mjs <n>
```

## 4. Report what happened

Your final message is what the human reads. Include:

- The PR link.
- The reviewer's findings from the dry run, in plain words and worst first —
  including which reviewer (Codex or Claude) wrote them. The human approved on
  the strength of those findings, so they must see them even though the label is
  already set.
- That the ticket is now `state:approved` (or, for a dry run, that nothing
  changed), and that `/ship <n>` is the next step whenever ready.
