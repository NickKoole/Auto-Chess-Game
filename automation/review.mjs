#!/usr/bin/env node
// Reviewer agent — read-only. Audits the PR for a ticket and posts findings.
//
//   node automation/review.mjs 42            review ticket 42's PR
//   node automation/review.mjs 42 --timeout 40
//
// Normally you don't run this yourself: dev.mjs spawns it as it exits. That
// direct hand-off is the whole "automatic review" feature — no watcher, no
// webhook, no polling.
//
// Two properties make the verdict worth anything, and both are structural
// rather than a matter of prompting:
//   * it runs in a FRESH session, so it has no memory of writing the code and
//     no investment in defending it;
//   * it is denied Write, Edit and gh (settings/review.json), so it cannot
//     quietly fix what it finds, and cannot approve its own subject.

import { randomUUID } from "node:crypto";
import { join } from "node:path";
import {
  MARK_REVIEW, ROOT, args, assertGh, comment, ensureWorktree, fmtDuration,
  getIssue, ghJson, loadSession, must, prComment, run, runClaude, saveSession,
  setState, template,
} from "./lib/core.mjs";

const PROMPTS = join(ROOT, "automation", "prompts");
const SETTINGS = join(ROOT, "automation", "settings", "review.json");
const DIFF_BUDGET = 120_000; // characters of diff sent inline
const a = args();

const ICON = { blocker: "🛑", major: "⚠️", minor: "🔸", nit: "💬" };
const ORDER = ["blocker", "major", "minor", "nit"];

/** Pull the trailing ```json fence out of the reviewer's final message. */
function parseVerdict(text) {
  const fences = [...text.matchAll(/```json\s*([\s\S]*?)```/gi)];
  for (let i = fences.length - 1; i >= 0; i--) {
    try {
      const v = JSON.parse(fences[i][1]);
      if (v.verdict !== "approve" && v.verdict !== "changes") continue;
      v.findings = (Array.isArray(v.findings) ? v.findings : []).filter((f) => f && f.issue);
      return v;
    } catch { /* try the fence before it */ }
  }
  return null;
}

function renderFindings(findings) {
  if (!findings.length) return "_Nothing to report._";
  return [...findings]
    .sort((x, y) => ORDER.indexOf(x.severity) - ORDER.indexOf(y.severity))
    .map((f) => {
      const where = f.file ? `\`${f.file}${f.line ? `:${f.line}` : ""}\`` : "_general_";
      return [
        `- ${ICON[f.severity] ?? "•"} **${f.severity}** · ${where}`,
        `  ${f.issue}`,
        f.fix ? `  _Suggested fix:_ ${f.fix}` : null,
      ].filter(Boolean).join("\n");
    })
    .join("\n");
}

async function main() {
  assertGh();

  const n = a.number();
  if (!n) {
    console.error("Usage: node automation/review.mjs <issue-number>");
    return 2;
  }

  const issue = getIssue(n);
  const session = loadSession(n);

  if (!session?.pr) {
    console.error(`No PR recorded for #${n} in .agent-state/. Run dev.mjs first.`);
    setState(n, "state:needs-you");
    return 1;
  }

  console.log(`reviewing #${n} — PR #${session.pr}`);
  setState(n, "state:reviewing");

  try {
    const wt = ensureWorktree(issue);

    // Review exactly what is on the remote branch, not whatever the local
    // worktree happens to be holding.
    must("git", ["fetch", "origin", wt.branch], { cwd: wt.dir });
    must("git", ["reset", "--hard", `origin/${wt.branch}`], { cwd: wt.dir });
    must("git", ["fetch", "origin", wt.base], { cwd: wt.dir });

    const range = `origin/${wt.base}...HEAD`;
    const stat = run("git", ["diff", "--stat", range], { cwd: wt.dir }).stdout;
    let diff = run("git", ["diff", range], { cwd: wt.dir }).stdout;

    if (!diff.trim()) {
      setState(n, "state:needs-you");
      prComment(session.pr, `${MARK_REVIEW}\n### 🤔 Nothing to review\n\n\`${wt.branch}\` has no changes against \`${wt.base}\`.`);
      return 1;
    }

    let truncated = false;
    if (diff.length > DIFF_BUDGET) {
      diff = diff.slice(0, DIFF_BUDGET);
      truncated = true;
      console.log(`diff truncated to ${DIFF_BUDGET} chars — reviewer will read files directly`);
    }

    const pr = ghJson(["pr", "view", String(session.pr), "--json", "body,url"]);

    const prompt = template(join(PROMPTS, "review.md"), {
      PR: String(session.pr),
      NUMBER: String(n),
      TITLE: issue.title,
      BRANCH: wt.branch,
      BASE: wt.base,
      BODY: issue.body || "(the ticket body is empty)",
      DEV_REPORT: (pr?.body ?? "").slice(0, 20_000) || "(none)",
      FILES: stat || "(none)",
      DIFF: diff + (truncated ? "\n\n[diff truncated — read the changed files directly for the rest]" : ""),
    });

    console.log("running claude (fresh session, read-only)…\n");
    const result = runClaude({
      cwd: wt.dir,
      prompt,
      settings: SETTINGS,
      systemPromptFile: join(PROMPTS, "review-system.md"),
      sessionId: randomUUID(), // always cold — never resumed
      timeoutMs: Number(a.get("--timeout", 30)) * 60_000,
    });

    console.log(`claude finished in ${fmtDuration(result.seconds)} (ok=${result.ok})`);

    if (!result.ok) {
      setState(n, "state:needs-you");
      prComment(session.pr, `${MARK_REVIEW}\n### ⚠️ Reviewer agent failed\n\n\`\`\`\n${result.text.slice(0, 6000)}\n\`\`\`\n\nThe code is fine — only the review didn't run. Re-run \`node automation/review.mjs ${n}\`.`);
      return 1;
    }

    const verdict = parseVerdict(result.text);
    if (!verdict) {
      setState(n, "state:needs-you");
      prComment(session.pr, [
        MARK_REVIEW,
        "### ⚠️ Reviewer produced no machine-readable verdict",
        "",
        "Its notes are below so nothing is lost — but treat them as unstructured.",
        "",
        result.text.slice(0, 30_000),
      ].join("\n"));
      return 1;
    }

    const blocking = verdict.findings.filter((f) => f.severity === "blocker" || f.severity === "major");
    const notes = result.text.replace(/```json[\s\S]*?```/gi, "").trim();

    prComment(session.pr, [
      MARK_REVIEW,
      verdict.verdict === "approve"
        ? "## ✅ Reviewer: no blocking findings"
        : `## 🔁 Reviewer: ${blocking.length} thing${blocking.length === 1 ? "" : "s"} to look at`,
      "",
      verdict.summary ?? "",
      "",
      "### Findings",
      "",
      renderFindings(verdict.findings),
      "",
      "<details><summary>Full reviewer notes</summary>",
      "",
      notes.slice(0, 40_000),
      "",
      "</details>",
      "",
      "---",
      "**This is not an approval** — GitHub review state stays yours, so an approval always means a human approved.",
      "",
      "Findings are advisory. Decide which ones matter, say so in a normal comment, then:",
      "",
      "```",
      `node automation/dev.mjs ${n}`,
      "```",
    ].join("\n"));

    setState(n, "state:needs-you");
    saveSession(n, { lastVerdict: verdict.verdict, lastReviewAt: new Date().toISOString() });

    console.log(`\n#${n} → state:needs-you`);
    console.log(`verdict: ${verdict.verdict} · ${verdict.findings.length} findings (${blocking.length} blocking)`);
    if (pr?.url) console.log(pr.url);
    return 0;
  } catch (err) {
    setState(n, "state:blocked");
    comment(n, `${MARK_REVIEW}\n### 💥 Reviewer agent crashed\n\n\`\`\`\n${String(err?.message ?? err).slice(0, 4000)}\n\`\`\``);
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
