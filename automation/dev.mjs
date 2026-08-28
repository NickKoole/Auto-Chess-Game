#!/usr/bin/env node
// Dev session. Set up an isolated worktree for a ticket, sit with Claude while
// it builds, then push a draft PR and hand straight to the reviewer.
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
// Safety does not come from watching, and never did: the agent is denied
// `git push`, `git remote` and `gh` outright (see settings/dev.json), so only
// this script publishes, and only onto a DRAFT PR on the ticket's own branch.

import { spawnSync } from "node:child_process";
import { createInterface } from "node:readline/promises";
import { randomUUID } from "node:crypto";
import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import {
  MARK_DEV, MARK_REVIEW, ROOT, areaOf, args, assertGh, clearSession, comment,
  commitsAhead, ensureWorktree, fmtDuration, getIssue, ghJson, isDirty,
  labelNames, listByState, loadSession, must, runClaudeInteractive, saveSession,
  setState, stateOf, template, timeOf,
} from "./lib/core.mjs";

// Scratch space inside the worktree: the task going in, the report coming out.
// Gitignored, so the commit sweep never picks it up.
const AGENT_DIR = ".agent";
const NO_REPORT = "_The session ended without a written report._";

const PROMPTS = join(ROOT, "automation", "prompts");
const SETTINGS = join(ROOT, "automation", "settings", "dev.json");
const a = args();

async function chooseIssue() {
  const explicit = a.number();
  if (explicit) return getIssue(explicit);

  const ready = listByState("state:ready").filter(
    (i) => !labelNames(i).includes("type:epic")
  );
  if (!ready.length) {
    console.log("Nothing in state:ready. File a ticket, or pass an issue number.");
    return null;
  }

  console.log("\nReady to build:\n");
  ready.forEach((i, idx) => console.log(`  ${String(idx + 1).padStart(2)}. #${i.number}  ${i.title}`));

  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const answer = (await rl.question("\nWhich one? (number, or blank to cancel) ")).trim();
  rl.close();
  if (!answer) return null;

  const pick = ready[Number(answer) - 1] ?? ready.find((i) => String(i.number) === answer);
  if (!pick) {
    console.log("Not a valid choice.");
    return null;
  }
  return getIssue(pick.number);
}

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
 * The report the agent left behind. An interactive session has no captured
 * output to mine for one — you read it as it happened — so the report is a file
 * it writes before finishing.
 */
function readReport(dir) {
  try {
    return readFileSync(join(dir, AGENT_DIR, "report.md"), "utf8").trim() || null;
  } catch {
    return null;
  }
}

/** Open a draft PR, or return the one that already exists for this branch. */
function ensurePr(issue, wt, report) {
  const found = ghJson(["pr", "list", "--head", wt.branch, "--state", "open", "--json", "number,url", "--limit", "1"]);
  if (found?.length) return found[0];

  const body = [
    report,
    "",
    "---",
    "🤖 Built in a dev session with a human in the loop, then reviewed automatically.",
    "Leave a normal comment with what you want changed, then re-run:",
    "",
    "```",
    `node automation/dev.mjs ${issue.number}`,
    "```",
    "",
    `Closes #${issue.number}`,
  ].join("\n");

  must("gh", [
    "pr", "create",
    "--draft",
    "--base", wt.base,
    "--head", wt.branch,
    "--title", issue.title,
    "--body-file", "-",
  ], { cwd: ROOT, input: body });

  const created = ghJson(["pr", "list", "--head", wt.branch, "--state", "open", "--json", "number,url", "--limit", "1"]);
  return created?.[0] ?? null;
}

function handOffToReview(issueNumber) {
  console.log("\n─── handing off to the reviewer ───\n");
  const res = spawnSync(
    process.execPath,
    [join(ROOT, "automation", "review.mjs"), String(issueNumber)],
    { cwd: ROOT, stdio: "inherit" }
  );
  return res.status ?? 1;
}

async function main() {
  assertGh();

  const issue = await chooseIssue();
  if (!issue) return 0;

  const n = issue.number;
  if (labelNames(issue).includes("type:epic")) {
    console.log(`#${n} is an epic — split it into child tickets first.`);
    return 1;
  }

  const prior = loadSession(n);
  const startFresh = a.has("--fresh") || !prior || prior.runs === 0;

  console.log(`\n#${n}  ${issue.title}`);
  console.log(`state: ${stateOf(issue) ?? "none"} · mode: ${startFresh ? "fresh build" : "continuing"}\n`);

  setState(n, "state:building");

  try {
    if (a.has("--fresh")) clearSession(n);

    const wt = ensureWorktree(issue);
    console.log(`worktree: ${wt.dir}`);
    console.log(`branch:   ${wt.branch} (from ${wt.base})`);

    const session = loadSession(n);
    const resuming = !startFresh && session?.sessionId && session.runs > 0;
    const sessionId = session?.sessionId ?? randomUUID();
    const feedback = resuming ? gatherFeedback(issue, session) : "";

    const prompt = template(join(PROMPTS, "dev.md"), {
      MODE: resuming
        ? "You have worked on this ticket before and your commits are already on the branch."
        : "You are starting this ticket from scratch.",
      NUMBER: String(n),
      TITLE: issue.title,
      AREA: areaOf(issue),
      BRANCH: wt.branch,
      BASE: wt.base,
      BODY: issue.body || "(the ticket body is empty)",
      FEEDBACK: feedback,
    });

    // The ticket goes in as a file rather than as one enormous argv entry: it
    // contains the whole issue body and every comment, which no shell should be
    // asked to quote. A stale report from a previous run has to go, or it would
    // be picked up as if it described this one.
    const agentDir = join(wt.dir, AGENT_DIR);
    mkdirSync(agentDir, { recursive: true });
    // Self-ignoring, so the commit sweep cannot pick this up even on a branch
    // cut before the root .gitignore knew about `.agent/`.
    writeFileSync(join(agentDir, ".gitignore"), "*\n");
    writeFileSync(join(agentDir, "task.md"), prompt);
    rmSync(join(agentDir, "report.md"), { force: true });

    console.log(`\nstarting claude — ${resuming ? "resuming your" : "a new"} session.`);
    console.log("You are in the loop: answer its questions, steer it, and exit when done.\n");

    const result = runClaudeInteractive({
      cwd: wt.dir,
      initialPrompt: `Read ${AGENT_DIR}/task.md and follow it.`,
      settings: SETTINGS,
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

    // Sweep up anything left uncommitted so no work is silently lost.
    if (isDirty(wt.dir)) {
      must("git", ["add", "-A"], { cwd: wt.dir });
      must("git", ["commit", "-m", `chore(agent): sweep uncommitted work for #${n}`], { cwd: wt.dir });
    }

    const ahead = commitsAhead(wt.dir, wt.base);

    const report = readReport(wt.dir);

    if (ahead === 0) {
      setState(n, "state:needs-you");
      console.log("\nNothing was committed. Nothing pushed.");
      comment(n, [
        MARK_DEV,
        "### 🤔 The dev session ended with no changes",
        "",
        report ?? "_No report was written._",
      ].join("\n"));
      return 1;
    }

    if (!report) {
      console.log(`(no ${AGENT_DIR}/report.md was written — the PR body will be a placeholder)`);
    }

    console.log(`pushing ${ahead} commit${ahead === 1 ? "" : "s"}…`);
    must("git", ["push", "-u", "origin", wt.branch], { cwd: wt.dir });

    const pr = ensurePr(issue, wt, report ?? NO_REPORT);
    if (pr) console.log(`PR: ${pr.url}`);

    saveSession(n, {
      sessionId: result.sessionId ?? sessionId,
      runs: (session?.runs ?? 0) + 1,
      pr: pr?.number ?? session?.pr ?? null,
      branch: wt.branch,
      base: wt.base,
      dir: wt.dir,
      lastSyncAt: new Date().toISOString(),
    });

    if (pr) {
      comment(n, [
        MARK_DEV,
        `### ✅ Dev session finished — PR #${pr.number}`,
        "",
        (report ?? NO_REPORT).slice(0, 50_000),
        "",
        "---",
        fmtDuration(result.seconds),
      ].join("\n"));
    }

    if (a.has("--no-review")) {
      setState(n, "state:needs-you");
      console.log("\n--no-review: stopping here.");
      return 0;
    }

    return handOffToReview(n);
  } catch (err) {
    setState(n, "state:blocked");
    comment(n, [
      MARK_DEV,
      "### 💥 Dev agent crashed",
      "",
      "```",
      String(err?.message ?? err).slice(0, 4000),
      "```",
    ].join("\n"));
    console.error(err);
    return 1;
  }
}

try {
  process.exit(await main());
} catch (err) {
  // Expected, user-fixable conditions (no gh auth, no remote) should read as a
  // sentence, not a stack trace.
  console.error("");
  console.error(err?.message ?? err);
  process.exit(1);
}
