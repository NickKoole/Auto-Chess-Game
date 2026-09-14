@AGENTS.md

## Claude Code

`AGENTS.md` is shared by Codex and Claude. Where it says "Codex conversation",
the same applies to a Claude Code session: use the claim → build → `finish.mjs`
path, and never start `automation/dev.mjs` or `session.mjs` from inside this
session — both open an interactive Codex attached to the terminal.

The ticket workflow is `/ready`, `/build <n>`, `/approve <n>` and `/ship <n>`
(`.claude/commands/`). Each runs its work in a Sonnet-pinned agent
(`.claude/agents/`): `/ready`, `/approve` and `/ship` in a `pipeline-runner`;
`/build` keeps the conversation with you in the main session and hands planning
and building to a `ticket-builder`. A ticket built with `/build` is reviewed by
a fresh headless Claude (`review.mjs --reviewer claude`); one built with
`dev.mjs` is reviewed by Codex.
