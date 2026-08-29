#!/usr/bin/env node
// Claim a ticket and stop. Worktree, branch, task file, push guard — everything
// the agent needs in front of it, and nothing that runs the agent.
//
//   node automation/claim.mjs 42          claim (or re-claim) ticket 42
//   node automation/claim.mjs             list the ready pool and pick one
//   node automation/claim.mjs 42 --fresh  discard the agent's memory, start cold
//
// `dev.mjs` imports this, runs Claude, then imports `finish.mjs`. Use the two
// halves directly when something else owns the session — an Orca terminal, a
// second machine, or a run you drive by hand. The session is deliberately not
// this script's business: what matters is that the worktree is set up the same
// way every time, and that only finish.mjs can publish out of it.

import { mkdirSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import {
  AGENT_DIR, DEV_SETTINGS, MARK_DEV, MARK_REVIEW, PROMPTS, areaOf, args, assertGh,
  chooseIssue, clearSession, comment, ensureWorktree, ghJson, installPushGuard,
  isEntrypoint, labelNames, loadSession, saveSession, setState, stateOf, template,
  timeOf,
} from "./lib/core.mjs";
import { randomUUID } from "node:crypto";

/** Everything you've said since the agent last ran, from the issue and the PR. */
function gatherFeedback(issue, session) {
  const since = session?.lastSyncAt ? Date.parse(session.lastSyncAt) : 0;
  const items = [];

  const take = (source) => (c) => {
    const body = (c.body ?? "").trim();
    if (!body) return;
    if (body.includes(MARK_DEV) || body.includes(MARK_REVIEW)) return; // our own output
    if (timeOf(c) <= since) return;
    items.push({ who: c.author?.login ?? "you", source, body });
  };

  (issue.comments ?? []).forEach(take("issue"));

  if (session?.pr) {
    const pr = ghJson(["pr", "view", String(session.pr), "--json", "comments,reviews"]);
    (pr?.comments ?? []).forEach(take("PR comment"));
    (pr?.reviews ?? []).forEach((r) => take(`PR review · ${r.state}`)(r));
  }

  if (!items.length) return "";

  return [
    "## Feedback to address",
    "",
    "You have already worked on this ticket. Address every point below, then re-verify.",
    "Anything not mentioned here should be left as it is.",
    "",
    ...items.map((i) => `**${i.who}** (${i.source}):\n\n> ${i.body.replace(/\n/g, "\n> ")}\n`),
  ].join("\n");
}

/**
 * Set up the ticket's worktree and return what the caller needs to run an agent
 * in it. Throws on anything that leaves the ticket unbuildable; the CLI wrapper
 * below turns that into `state:blocked` and a comment on the issue.
 */
export async function claim({ number = null, fresh = false } = {}) {
  assertGh();

  const issue = await chooseIssue(number);
  if (!issue) return null;

  const n = issue.number;
  if (labelNames(issue).includes("type:epic")) {
    // Refusal, not failure: say so and leave the ticket exactly as it was.
    console.log(`#${n} is an epic — split it into child tickets first.`);
    return null;
  }

  const prior = loadSession(n);
  const startFresh = fresh || !prior || prior.runs === 0;

  console.log(`\n#${n}  ${issue.title}`);
  console.log(`state: ${stateOf(issue) ?? "none"} · mode: ${startFresh ? "fresh build" : "continuing"}\n`);

  setState(n, "state:building");
  if (fresh) clearSession(n);

  const wt = ensureWorktree(issue);
  installPushGuard(wt.dir);

  // The ticket goes in as a file rather than as one enormous argv entry: it
  // contains the whole issue body and every comment, which no shell should be
  // asked to quote. A stale report from a previous run has to go, or it would
  // be picked up as if it described this one.
  const agentDir = join(wt.dir, AGENT_DIR);
  mkdirSync(agentDir, { recursive: true });
  // Self-ignoring, so the commit sweep cannot pick this up even on a branch
  // cut before the root .gitignore knew about `.agent/`.
  writeFileSync(join(agentDir, ".gitignore"), "*\n");
  rmSync(join(agentDir, "report.md"), { force: true });

  const session = loadSession(n);
  const resuming = !startFresh && Boolean(session?.sessionId) && session.runs > 0;
  const sessionId = session?.sessionId ?? randomUUID();

  writeFileSync(join(agentDir, "task.md"), template(join(PROMPTS, "dev.md"), {
    MODE: resuming
      ? "You have worked on this ticket before and your commits are already on the branch."
      : "You are starting this ticket from scratch.",
    NUMBER: String(n),
    TITLE: issue.title,
    AREA: areaOf(issue),
    BRANCH: wt.branch,
    BASE: wt.base,
    BODY: issue.body || "(the ticket body is empty)",
    FEEDBACK: resuming ? gatherFeedback(issue, session) : "",
  }));

  saveSession(n, {
    sessionId,
    runs: session?.runs ?? 0,
    branch: wt.branch,
    base: wt.base,
    dir: wt.dir,
  });

  return { issue, wt, sessionId, resuming };
}

async function main() {
  const a = args();
  // Anything thrown from here is a genuine failure, and the handler below
  // marks the ticket blocked. A refusal returns null instead.
  const claimed = await claim({ number: a.number(), fresh: a.has("--fresh") });
  if (!claimed) return 0;

  const { issue, wt } = claimed;
  console.log(`worktree: ${wt.dir}`);
  console.log(`branch:   ${wt.branch} (from ${wt.base})`);
  console.log("\nStart an agent in that directory. To keep the permission fence and");
  console.log("the pinned model, launch it as:\n");
  console.log(`  claude --settings ${DEV_SETTINGS}`);
  console.log(`\nand point it at ${AGENT_DIR}/task.md. When the session ends:\n`);
  console.log(`  node automation/finish.mjs ${issue.number}\n`);
  console.log("The worktree refuses any push that does not come from finish.mjs,");
  console.log("so a session launched without those settings still cannot publish.");
  return 0;
}

if (isEntrypoint(import.meta.url)) {
  const a = args();
  try {
    process.exit(await main());
  } catch (err) {
    const n = a.number();
    if (n) {
      setState(n, "state:blocked");
      comment(n, [MARK_DEV, "### 💥 Claim failed", "", "```", String(err?.message ?? err).slice(0, 4000), "```"].join("\n"));
    }
    console.error(err);
    process.exit(1);
  }
}
