#!/usr/bin/env node
// Close out a ticket's build. Sweep, push, draft PR, hand to the reviewer.
//
//   node automation/finish.mjs 42              publish and review
//   node automation/finish.mjs 42 --no-review  stop after the PR
//
// The other half of `claim.mjs`. It does not care what ran the session — an
// attached `dev.mjs` run, an Orca terminal, or you — only what is committed on
// the branch. This is the only thing that may push out of an agent worktree:
// claim.mjs installs a pre-push hook that refuses everything else.

import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  AGENT_DIR, MARK_DEV, PUSH_ENV, ROOT, args, assertGh, comment, commitsAhead,
  fmtDuration, getIssue, gh, ghJson, isDirty, isEntrypoint, loadSession, must,
  saveSession, setState,
} from "./lib/core.mjs";

const NO_REPORT = "_The session ended without a written report._";

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

const findPr = (branch) =>
  ghJson(["pr", "list", "--head", branch, "--state", "open", "--json", "number,url", "--limit", "1"])?.[0] ?? null;

/**
 * The PR description: this run's report, then the standing footer.
 *
 * Assembled here and nowhere else. It gets written twice — once when the PR is
 * opened, and again on every revision run — and the footer carries
 * `Closes #<issue>`. A body rebuilt without that trailer stops closing its
 * ticket on merge and says nothing about having done so, which is exactly the
 * kind of drift two copies of this list would eventually produce.
 */
export function prBody(issueNumber, report) {
  return [
    report ?? NO_REPORT,
    "",
    "---",
    "🤖 Built in a dev session with a human in the loop, then reviewed automatically.",
    "Leave a normal comment with what you want changed, then re-run:",
    "",
    "```",
    `node automation/dev.mjs ${issueNumber}`,
    "```",
    "",
    `Closes #${issueNumber}`,
  ].join("\n");
}

/**
 * Open a draft PR, or bring the existing one's description up to date.
 *
 * A revision run pushes onto a PR that already exists, and used to leave its
 * description showing the first round. That is worse than a missing
 * description: the reviewer checks the diff against it and a human approves on
 * it, so a stale one misleads both rather than merely telling them nothing.
 */
function ensurePr(issue, wt, report) {
  const existing = findPr(wt.branch);

  if (existing) {
    // A run with no report has nothing better to say than what is already
    // there. Stamping the placeholder over a good description would discard the
    // previous round's for nothing.
    if (report) {
      gh(["pr", "edit", String(existing.number), "--body-file", "-"], {
        cwd: ROOT,
        input: prBody(issue.number, report),
      });
      console.log(`refreshed PR #${existing.number} description from this run's report`);
    } else {
      console.log(`(no report this run — PR #${existing.number} keeps its description)`);
    }

    return existing;
  }

  must("gh", [
    "pr", "create",
    "--draft",
    "--base", wt.base,
    "--head", wt.branch,
    "--title", issue.title,
    "--body-file", "-",
  ], { cwd: ROOT, input: prBody(issue.number, report) });

  return findPr(wt.branch);
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

/**
 * `session` is what a session runner knows and this script cannot find out for
 * itself: how long it took and which Claude session id it was. Both are absent
 * when something other than dev.mjs ran the agent, and nothing here needs them.
 */
export function finish(n, { review = true, session: sessionResult = null } = {}) {
  assertGh();

  const issue = getIssue(n);
  const stored = loadSession(n);
  if (!stored?.dir) {
    throw new Error(`No worktree recorded for #${n}. Run: node automation/claim.mjs ${n}`);
  }

  const wt = { dir: stored.dir, branch: stored.branch, base: stored.base };

  // A branch that is not this ticket's would mean the worktree was reused for
  // something else; publishing it under this issue would be wrong.
  const head = must("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: wt.dir });
  if (head !== wt.branch) {
    throw new Error(`#${n}'s worktree is on ${head}, expected ${wt.branch}. Refusing to publish.`);
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
  must("git", ["push", "-u", "origin", wt.branch], { cwd: wt.dir, env: PUSH_ENV });

  // The raw report, not `report ?? NO_REPORT`: ensurePr has to tell "this run
  // wrote nothing" apart from "this run wrote the placeholder", because only
  // the first should leave an existing description alone.
  const pr = ensurePr(issue, wt, report);
  if (pr) console.log(`PR: ${pr.url}`);

  saveSession(n, {
    sessionId: sessionResult?.sessionId ?? stored.sessionId,
    runs: (stored.runs ?? 0) + 1,
    pr: pr?.number ?? stored.pr ?? null,
    lastSyncAt: new Date().toISOString(),
  });

  if (pr) {
    comment(n, [
      MARK_DEV,
      `### ✅ Dev session finished — PR #${pr.number}`,
      "",
      (report ?? NO_REPORT).slice(0, 50_000),
      ...(sessionResult?.seconds ? ["", "---", fmtDuration(sessionResult.seconds)] : []),
    ].join("\n"));
  }

  if (!review) {
    setState(n, "state:needs-you");
    console.log("\n--no-review: stopping here.");
    return 0;
  }

  return handOffToReview(n);
}

if (isEntrypoint(import.meta.url)) {
  const a = args();
  const n = a.number();
  if (!n) {
    console.error("Which ticket? Usage: node automation/finish.mjs <issue>");
    process.exit(1);
  }
  try {
    process.exit(finish(n, { review: !a.has("--no-review") }));
  } catch (err) {
    setState(n, "state:blocked");
    comment(n, [MARK_DEV, "### 💥 Publishing failed", "", "```", String(err?.message ?? err).slice(0, 4000), "```"].join("\n"));
    console.error(err);
    process.exit(1);
  }
}
