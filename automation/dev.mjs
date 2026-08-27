#!/usr/bin/env node
// Dev agent — headless. Build a ticket, push a draft PR, then hand straight to
// the reviewer.
//
//   node automation/dev.mjs 42          build (or continue) ticket 42
//   node automation/dev.mjs             list the ready pool and pick one
//   node automation/dev.mjs 42 --fresh  discard the agent's memory, start cold
//   node automation/dev.mjs 42 --no-review   stop after the PR
//   node automation/dev.mjs 42 --timeout 90  minutes, default 45
//
// The run is unattended by design: you fire it and walk away. Safety comes from
// the destination, not from supervision — work lands on a DRAFT PR on its own
// branch, and the agent is denied `git push` and `gh` outright (see
// settings/dev.json), so only this script can publish anything.

import { spawnSync } from "node:child_process";
import { createInterface } from "node:readline/promises";
import { randomUUID } from "node:crypto";
import { join } from "node:path";
import {
  MARK_DEV, MARK_REVIEW, ROOT, areaOf, args, assertGh, clearSession, comment,
  commitsAhead, ensureWorktree, fmtDuration, getIssue, ghJson, isDirty,
  labelNames, listByState, loadSession, must, runClaude, saveSession, setState,
  stateOf, template, timeOf,
} from "./lib/core.mjs";

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

/** Open a draft PR, or return the one that already exists for this branch. */
function ensurePr(issue, wt, report) {
  const found = ghJson(["pr", "list", "--head", wt.branch, "--state", "open", "--json", "number,url", "--limit", "1"]);
  if (found?.length) return found[0];

  const body = [
    report,
    "",
    "---",
    "🤖 Built headless by the dev agent, then reviewed automatically.",
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

    if (resuming && !feedback) {
      console.log("\nNo new feedback since the last run — nothing to act on.");
      console.log("Comment on the PR with what you want changed, then re-run.");
      setState(n, "state:needs-you");
      return 0;
    }

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

    console.log(`\nrunning claude (headless, ${resuming ? "resumed" : "new"} session)…\n`);
    const result = runClaude({
      cwd: wt.dir,
      prompt,
      settings: SETTINGS,
      systemPromptFile: join(PROMPTS, "dev-system.md"),
      sessionId: resuming ? null : sessionId,
      resume: resuming ? sessionId : null,
      timeoutMs: Number(a.get("--timeout", 45)) * 60_000,
    });

    console.log(`claude finished in ${fmtDuration(result.seconds)} (ok=${result.ok})`);

    if (!result.ok) {
      setState(n, "state:needs-you");
      comment(n, [
        MARK_DEV,
        "### ⚠️ Dev agent run failed",
        "",
        "```",
        result.text.slice(0, 6000),
        "```",
        "",
        "Nothing was pushed. Comment with guidance and re-run, or use `--fresh` to start cold.",
      ].join("\n"));
      return 1;
    }

    // Sweep up anything left uncommitted so no work is silently lost.
    if (isDirty(wt.dir)) {
      must("git", ["add", "-A"], { cwd: wt.dir });
      must("git", ["commit", "-m", `chore(agent): sweep uncommitted work for #${n}`], { cwd: wt.dir });
    }

    const ahead = commitsAhead(wt.dir, wt.base);
    if (ahead === 0) {
      setState(n, "state:needs-you");
      comment(n, [
        MARK_DEV,
        "### 🤔 The dev agent produced no changes",
        "",
        "It reported:",
        "",
        result.text.slice(0, 4000),
        "",
        "This usually means the acceptance criteria aren't concrete enough to act on.",
      ].join("\n"));
      return 1;
    }

    console.log(`pushing ${ahead} commit${ahead === 1 ? "" : "s"}…`);
    must("git", ["push", "-u", "origin", wt.branch], { cwd: wt.dir });

    const pr = ensurePr(issue, wt, result.text);
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
      const summary = [
        MARK_DEV,
        `### ✅ Dev agent finished — PR #${pr.number}`,
        "",
        result.text.slice(0, 50_000),
        "",
        "---",
        `${result.turns ?? "?"} turns · ${fmtDuration(result.seconds)}` +
          (result.cost != null ? ` · $${result.cost.toFixed(2)}` : ""),
      ].join("\n");
      comment(n, summary);
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
