#!/usr/bin/env node
// Dev session. Claim a ticket, sit with Claude while it builds, then publish.
//
//   node automation/dev.mjs 42          build (or continue) ticket 42
//   node automation/dev.mjs             list the ready pool and pick one
//   node automation/dev.mjs 42 --fresh  discard the agent's memory, start cold
//   node automation/dev.mjs 42 --no-review   stop after the PR
//
// You are in the loop for the whole build: Claude runs attached to this
// terminal, so it asks you directly rather than guessing and explaining itself
// afterwards. Everything around the session is still automated — worktree,
// commit sweep, push, draft PR, review.
//
// This script is only the one-terminal path through claim.mjs and finish.mjs.
// It holds no logic of its own, so running those two around some other session
// — an Orca terminal, a second machine, several tickets at once — behaves
// identically. Safety does not come from watching, and never did: the agent is
// denied `git push`, `git remote` and `gh` outright (see settings/dev.json),
// and claim.mjs installs a pre-push hook so the worktree refuses to publish
// through anything but finish.mjs. Everything lands on a DRAFT PR on the
// ticket's own branch.

import { join } from "node:path";
import {
  AGENT_DIR, DEV_SETTINGS, MARK_DEV, PROMPTS, args, comment, fmtDuration,
  isEntrypoint, runClaudeInteractive, setState,
} from "./lib/core.mjs";
import { claim } from "./claim.mjs";
import { finish } from "./finish.mjs";

async function main() {
  const a = args();

  const claimed = await claim({ number: a.number(), fresh: a.has("--fresh") });
  if (!claimed) return 0;

  const { issue, wt, sessionId, resuming } = claimed;
  console.log(`worktree: ${wt.dir}`);
  console.log(`branch:   ${wt.branch} (from ${wt.base})`);

  console.log(`\nstarting claude — ${resuming ? "resuming your" : "a new"} session.`);
  console.log("You are in the loop: answer its questions, steer it, and exit when done.\n");

  const result = runClaudeInteractive({
    cwd: wt.dir,
    initialPrompt: `Read ${AGENT_DIR}/task.md and follow it.`,
    settings: DEV_SETTINGS,
    systemPromptFile: join(PROMPTS, "dev-system.md"),
    sessionId: resuming ? null : sessionId,
    resume: resuming ? sessionId : null,
  });

  console.log(`\nsession ended after ${fmtDuration(result.seconds)}`);
  if (result.code !== 0) {
    // Not fatal. Everything below lands on a draft branch, and losing work
    // because the session exited untidily would be the worse outcome.
    console.log(`(claude exited ${result.code} — continuing with whatever was committed)`);
  }

  return finish(issue.number, {
    review: !a.has("--no-review"),
    session: { sessionId: result.sessionId ?? sessionId, seconds: result.seconds },
  });
}

// Guarded like claim.mjs and finish.mjs. Without it, merely importing this
// module claims a ticket and opens a Claude session as a side effect — running
// the whole pipeline on whatever happens to be in state:ready.
if (isEntrypoint(import.meta.url)) {
  try {
    process.exit(await main());
  } catch (err) {
    const n = args().number();
    if (n) {
      setState(n, "state:blocked");
      comment(n, [
        MARK_DEV,
        "### 💥 Dev agent crashed",
        "",
        "```",
        String(err?.message ?? err).slice(0, 4000),
        "```",
      ].join("\n"));
    }
    console.error(err);
    process.exit(1);
  }
}
