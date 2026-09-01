---
description: Set state:approved on a ticket from here instead of the GitHub web UI. Shows the PR and the reviewer's findings first — that display is the checkpoint, not a confirmation prompt.
argument-hint: <issue-number> [--dry-run]
allowed-tools: Bash(node automation/approve.mjs:*), Bash(gh issue list:*)
---

Approve ticket `$ARGUMENTS`.

All of the logic lives in `automation/approve.mjs`. This is a convenience over
`gh issue edit --add-label state:approved` — it exists so you don't have to
switch to the browser, not so the machine can talk itself into a label it
isn't allowed to set on its own. See `automation/README.md` and `ship.mjs` for
why no other script in this pipeline writes `state:approved`: it is the one
point where a human says yes, and this command only runs when you type it.

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
nothing. Read the findings before doing anything else — that's the actual
review step; the label is just recording that it happened.

If there's no PR yet, or no reviewer comment, relay that and stop — there's
nothing to approve.

## 3. Approve it

Typing `/approve <n>` **is** the go-ahead, same as `/ship`. Do not follow the
dry run with an AskUserQuestion re-asking whether to approve — that already
happened when you read the findings in step 2. Run:

```
node automation/approve.mjs <n>
```

## 4. Report what happened

Say the ticket is now `state:approved` and that `/ship <n>` is the next step
whenever ready.
