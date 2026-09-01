#!/usr/bin/env node
// Set state:approved on a ticket, from the terminal instead of the GitHub web UI.
//
//   node automation/approve.mjs 42            show the PR + review, then approve
//   node automation/approve.mjs 42 --dry-run  show the PR + review, change nothing
//
// This does not weaken the approval gate described in ship.mjs. Every other
// label in the pipeline is written by an unattended script — claim.mjs,
// finish.mjs, review.mjs all run without you watching. This one only runs when
// a human types the command, same as clicking the button on GitHub; it just
// saves the tab switch. It prints the PR and the reviewer's last findings
// first, on purpose — approving without looking at either defeats the point
// of the label.

import {
  args, assertGh, getIssue, ghJson, isEntrypoint, loadSession, MARK_REVIEW,
  setState, stateOf,
} from "./lib/core.mjs";

const findPr = (branch) =>
  ghJson([
    "pr", "list", "--head", branch, "--state", "all",
    "--json", "number,url,state,isDraft",
    "--limit", "1",
  ])?.[0] ?? null;

const lastReview = (pr) =>
  ghJson(["pr", "view", String(pr), "--json", "comments"])
    ?.comments?.filter((c) => c.body?.includes(MARK_REVIEW))
    ?.at(-1) ?? null;

export async function approve(n, { dryRun = false } = {}) {
  assertGh();

  const issue = getIssue(n);
  if (!issue) throw new Error("No issue #" + n + ".");

  const state = stateOf(issue);
  console.log("\n#" + n + "  " + issue.title);
  console.log("state: " + (state ?? "unlabelled"));

  const session = loadSession(n);
  const pr = session?.branch ? findPr(session.branch) : null;
  if (!pr) {
    throw new Error("No PR found for #" + n + ". Nothing to approve yet.");
  }
  console.log("PR:    " + pr.url + (pr.isDraft ? "  (draft)" : ""));

  const review = lastReview(pr.number);
  if (review) {
    console.log("\nlatest reviewer findings:\n");
    console.log(review.body);
  } else {
    console.log("\nno reviewer comment found on this PR — nothing has audited the diff yet.");
  }

  if (state === "state:approved") {
    console.log("\nalready state:approved. Nothing to do.");
    return 0;
  }

  if (dryRun) {
    console.log("\n--dry-run: would set state:approved on #" + n + ".");
    return 0;
  }

  setState(n, "state:approved");
  console.log("\n#" + n + " → state:approved. Run `node automation/ship.mjs " + n + "` when ready.");
  return 0;
}

if (isEntrypoint(import.meta.url)) {
  const a = args();
  const n = a.number();
  if (!n) {
    console.error("Which ticket? Usage: node automation/approve.mjs <issue> [--dry-run]");
    process.exit(1);
  }
  try {
    process.exit(await approve(n, { dryRun: a.has("--dry-run") }));
  } catch (err) {
    console.error("\n" + (err?.message ?? err));
    process.exit(1);
  }
}
