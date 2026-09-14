#!/usr/bin/env node
// Dev session. Claim a ticket, sit with Codex while it builds, then publish.
//
//   node automation/dev.mjs 42          build (or continue) ticket 42
//   node automation/dev.mjs             list the ready pool and pick one
//   node automation/dev.mjs 42 --fresh  discard the agent's memory, start cold
//   node automation/dev.mjs 42 --no-review   stop after the PR
//
// You are in the loop for the whole build: Codex runs attached to this
// terminal, so it asks you directly rather than guessing and explaining itself
// afterwards. Everything around the session is still automated — worktree,
// commit sweep, push, draft PR, review.
//
import { MARK_DEV, args, comment, fmtDuration, isEntrypoint, setState } from "./lib/core.mjs";
import { claim } from "./claim.mjs";
import { finish } from "./finish.mjs";
import { runTicketSession } from "./session.mjs";

async function main() {
  const a = args();

  const claimed = await claim({ number: a.number(), fresh: a.has("--fresh") });
  if (!claimed) return 0;

  const { issue, wt, resuming } = claimed;
  console.log(`worktree: ${wt.dir}`);
  console.log(`branch:   ${wt.branch} (from ${wt.base})`);

  console.log(`\nstarting codex — ${resuming ? "resuming your" : "a new"} session.`);
  console.log("You are in the loop: answer its questions, steer it, and exit when done.\n");

  const result = runTicketSession(issue.number);

  console.log(`\nsession ended after ${fmtDuration(result.seconds)}`);
  if (result.code !== 0) {
    setState(issue.number, "state:needs-you");
    console.error(`Codex exited ${result.code}. Work is saved locally; publishing was skipped.`);
    return result.code;
  }

  return finish(issue.number, {
    review: !a.has("--no-review"),
    reviewer: "codex",
    session:{ codexSessionId: result.sessionId, seconds: result.seconds },
  });
}

// Guarded like claim.mjs and finish.mjs. Without it, merely importing this
// module claims a ticket and opens a Codex session as a side effect — running
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
