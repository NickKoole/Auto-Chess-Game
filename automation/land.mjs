#!/usr/bin/env node
// Land an approved ticket: push what is left, take the PR out of draft, wait
// for CI, squash-merge, and clear away the worktree.
//
//   node automation/land.mjs 3              land ticket 3
//   node automation/land.mjs 3 --dry-run    check every precondition, change nothing
//   node automation/land.mjs 3 --keep       merge, but leave the worktree in place
//   node automation/land.mjs 3 --timeout 30 how long to wait for CI, in minutes
//
// The last step of the pipeline, and the only one that is destructive on
// purpose. Two labels it deliberately does NOT write:
//
//   state:approved  is yours. Nothing in the pipeline sets it — not claim.mjs,
//                   not finish.mjs, not review.mjs — because it is the single
//                   point where a human says yes. This script *requires* it and
//                   refuses without it. A command that could set it and merge in
//                   one breath would let the machine approve its own work.
//
//   state:done      is already handled. .github/workflows/pr-closed.yml reads
//                   the "Closes #N" trailer when the PR merges and settles the
//                   label from there, so writing it here would race a workflow
//                   that is already correct.

import { existsSync, readdirSync, rmSync } from "node:fs";
import { join } from "node:path";
import {
  PUSH_ENV, ROOT, args, assertGh, clearSession, getIssue, gh, ghJson, isDirty,
  isEntrypoint, loadSession, must, run, stateOf,
} from "./lib/core.mjs";

const REQUIRED_STATE = "state:approved";

/** Check conclusions that are not a red light. A skipped conditional job is a pass. */
const CHECK_PASSED = new Set(["SUCCESS", "NEUTRAL", "SKIPPED"]);

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const findPr = (branch) =>
  ghJson([
    "pr", "list", "--head", branch, "--state", "all",
    "--json", "number,url,state,isDraft,mergeable,mergeStateStatus",
    "--limit", "1",
  ])?.[0] ?? null;

/**
 * gh reports two shapes in one array: CheckRun (a workflow job) and
 * StatusContext (a legacy commit status). Normalise them — deciding whether to
 * merge is the wrong moment to trip over a schema difference.
 */
const checkName = (c) => c.name ?? c.context ?? "check";

function checkState(c) {
  if (c.__typename === "StatusContext") return c.state ?? "PENDING";
  if (c.status !== "COMPLETED") return "PENDING";
  return c.conclusion ?? "PENDING";
}

const rollup = (pr) =>
  ghJson(["pr", "view", String(pr), "--json", "statusCheckRollup"])?.statusCheckRollup ?? [];

/**
 * Block until CI is green, and throw if it is not.
 *
 * This is the gap the script exists to close. The reviewer audits the diff, so
 * until now nothing in the pipeline has looked at whether the branch actually
 * builds — a ticket can be read, approved and merged while CI is red.
 */
async function waitForChecks(pr, { timeoutMs, intervalMs = 15_000 }) {
  const deadline = Date.now() + timeoutMs;
  let announced = false;

  for (;;) {
    const checks = rollup(pr);

    if (checks.length === 0) {
      // CI runs on pull_request, so an empty rollup means the run has not
      // registered yet — or that no workflow matched, which is a thing to go and
      // look at rather than to merge past.
      if (Date.now() > deadline) {
        throw new Error(
          "No CI checks reported on PR #" + pr + " within the timeout. " +
          "Nothing has verified this branch — look before merging."
        );
      }
      await sleep(intervalMs);
      continue;
    }

    const states = checks.map((c) => [checkName(c), checkState(c)]);
    const failed = states.filter(([, s]) => !CHECK_PASSED.has(s) && s !== "PENDING");
    const pending = states.filter(([, s]) => s === "PENDING");

    if (failed.length > 0) {
      throw new Error(
        "CI is not green on PR #" + pr + ":\n" +
        failed.map(([n, s]) => "  " + n + ": " + s).join("\n")
      );
    }

    if (pending.length === 0) {
      console.log("CI green (" + states.length + " check" + (states.length === 1 ? "" : "s") + ").");
      return;
    }

    if (!announced) {
      console.log("waiting for CI: " + pending.map(([n]) => n).join(", ") + "…");
      announced = true;
    }

    if (Date.now() > deadline) {
      throw new Error(
        "CI still running after the timeout: " + pending.map(([n]) => n).join(", ") +
        ". Re-run land.mjs when it settles, or pass --timeout <minutes>."
      );
    }

    await sleep(intervalMs);
  }
}

/**
 * Everything that must hold before anything irreversible happens, gathered in
 * one pass so a refusal names every problem it can see rather than making you
 * discover them one run at a time.
 */
function preflight(n) {
  const issue = getIssue(n);
  if (!issue) throw new Error("No issue #" + n + ".");

  const problems = [];
  const session = loadSession(n);
  if (!session?.branch) {
    problems.push("No worktree recorded for #" + n + ". Was it ever claimed?");
    return { issue, session, pr: null, worktreeLives: false, problems };
  }

  // A live worktree, not merely a directory that exists: an interrupted removal
  // leaves the now-empty folder behind with no .git in it, and reading HEAD out
  // of that would refuse the landing over a directory holding nothing.
  const worktreeLives = Boolean(session.dir) && existsSync(join(session.dir, ".git"));

  if (worktreeLives) {
    const head = run("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: session.dir }).stdout;
    if (head !== session.branch) {
      problems.push(session.dir + " is on " + head + ", expected " + session.branch + ".");
    }
    if (isDirty(session.dir)) {
      problems.push(
        session.dir + " has uncommitted changes. Commit them and re-run finish.mjs, " +
        "or discard them — landing would leave them behind with nowhere to go."
      );
    }
  }

  const pr = findPr(session.branch);
  if (!pr) {
    problems.push("No PR for " + session.branch + ". Run: node automation/finish.mjs " + n);
  } else if (pr.state === "CLOSED") {
    problems.push("PR #" + pr.number + " was closed without merging.");
  } else if (pr.state === "OPEN" && pr.mergeable !== "MERGEABLE") {
    problems.push(
      "PR #" + pr.number + " is not mergeable (" + pr.mergeable + ", " +
      pr.mergeStateStatus + "). A conflict with main is the usual cause."
    );
  }

  // The approval gate covers merging, and only merging. Once the PR is in, the
  // ticket is state:done and the worktree is just litter — gating the tidy-up
  // on a label the merge already consumed would strand exactly the checkouts
  // this script exists to clear away.
  const state = stateOf(issue);

  if (pr?.state !== "MERGED" && state !== REQUIRED_STATE) {
    problems.push(
      "#" + n + " is " + (state ?? "unlabelled") + ", not " + REQUIRED_STATE +
      ". Approving is yours to do: read the PR, then label it."
    );
  }

  return { issue, session, pr, worktreeLives, problems };
}

/**
 * Remove the worktree, its branch and its state file.
 *
 * Nothing else in the pipeline does this, so worktrees for merged tickets pile
 * up until someone notices. Deliberately not forced: if git says the checkout
 * still holds something, that is a fact worth reading rather than overriding, so
 * this prints the command and leaves it to you.
 */
function cleanUp(n, session, worktreeLives) {
  let orphanedDir = null;

  if (worktreeLives) {
    const removed = run("git", ["worktree", "remove", session.dir], { cwd: ROOT });

    if (removed.code !== 0) {
      // A non-zero exit does not mean nothing happened. git deletes the files
      // and deregisters the worktree before unlinking the directory itself, so
      // this fails in two very different situations: the checkout still holds
      // something, or everything is gone and only the empty directory survived
      // — which on Windows means a shell is sitting in it. Ask git which,
      // rather than reading the exit code as the worse of the two.
      run("git", ["worktree", "prune"], { cwd: ROOT });

      const registered = run("git", ["worktree", "list", "--porcelain"], { cwd: ROOT })
        .stdout.replaceAll("\\", "/")
        .includes(session.dir.replaceAll("\\", "/"));

      if (registered) {
        console.log("\n! could not remove the worktree: " + (removed.stderr || removed.stdout));
        console.log("  it still holds something. Look, then:  git worktree remove --force " + session.dir);
        // Leaving the branch alone is the point: it is still checked out there.
        return false;
      }

      orphanedDir = session.dir;
    } else {
      console.log("removed worktree " + session.dir);
    }
  }

  // -D, not -d: a squash merge rewrites the commits, so git cannot see this
  // branch as merged and -d would refuse it. The work is on main under a new
  // sha, which is what makes the capital D safe here and nowhere else.
  run("git", ["branch", "-D", session.branch], { cwd: ROOT });
  run("git", ["fetch", "origin", "--prune"], { cwd: ROOT });
  clearSession(n);
  console.log("cleared .agent-state/issue-" + n + ".json");

  // Either route can leave an empty directory behind: this run's rmdir failing,
  // or an earlier interrupted one. Only ever removed when it is genuinely
  // empty — deleting a directory that still holds files is not this script's
  // business, however merged the branch is.
  const leftover = orphanedDir ?? session.dir;

  if (leftover && existsSync(leftover) && readdirSync(leftover).length === 0) {
    try {
      rmSync(leftover, { recursive: false });
      console.log("removed the empty directory " + leftover);
    } catch {
      console.log(
        "\n! git removed the worktree, but the now-empty directory survived:\n  " +
        leftover + "\n  Something has it open — on Windows, usually a shell sitting in it." +
        "\n  Nothing is inside it; delete it whenever convenient."
      );
    }
  }

  return true;
}

export async function land(n, { dryRun = false, keep = false, timeoutMinutes = 20 } = {}) {
  assertGh();

  const { issue, session, pr, worktreeLives, problems } = preflight(n);

  console.log("\n#" + n + "  " + issue.title);
  if (pr) console.log("PR:  " + pr.url + (pr.state === "MERGED" ? "  (already merged)" : ""));

  if (problems.length > 0) {
    console.log("\nNot landing:\n");
    problems.forEach((p) => console.log("  · " + p));
    return 1;
  }

  if (dryRun) {
    console.log("\n--dry-run: every precondition passes.");
    if (pr.state === "MERGED") {
      console.log(keep
        ? "Nothing to do: already merged, and --keep leaves the worktree alone."
        : "Would clear away the worktree, its branch and its state file. Nothing to merge.");
    } else {
      console.log("Would push, ready, wait for CI, squash-merge, and " +
        (keep ? "keep the worktree." : "clean up the worktree."));
    }
    return 0;
  }

  // Re-running after an interrupted landing should finish the job, not refuse.
  if (pr.state === "MERGED") {
    console.log("\nalready merged — clearing away what is left.");
    return keep || cleanUp(n, session, worktreeLives) ? 0 : 1;
  }

  if (worktreeLives) {
    const ahead = run(
      "git", ["rev-list", "--count", "origin/" + session.branch + "..HEAD"], { cwd: session.dir }
    );
    const unpushed = ahead.code === 0 ? Number(ahead.stdout) || 0 : 0;

    if (unpushed > 0) {
      // The worktree's pre-push hook refuses anything without AGENT_PUSH_OK, so
      // this needs the same env finish.mjs uses. That guard exists to stop an
      // agent publishing mid-session, not to stop the pipeline finishing.
      console.log("pushing " + unpushed + " unpushed commit" + (unpushed === 1 ? "" : "s") + "…");
      must("git", ["push", "origin", session.branch], { cwd: session.dir, env: PUSH_ENV });
    }
  }

  if (pr.isDraft) {
    gh(["pr", "ready", String(pr.number)]);
    console.log("PR #" + pr.number + " is out of draft.");
  }

  await waitForChecks(pr.number, { timeoutMs: timeoutMinutes * 60_000 });

  gh(["pr", "merge", String(pr.number), "--squash"]);
  console.log("\nmerged PR #" + pr.number + ".");

  // Deleting the remote branch is GitHub's to do and not worth failing over: the
  // merge has already happened by the time this runs.
  run("gh", ["api", "-X", "DELETE", "repos/{owner}/{repo}/git/refs/heads/" + session.branch]);

  const cleaned = keep ? true : cleanUp(n, session, worktreeLives);
  if (keep) console.log("--keep: " + session.dir + " left in place.");

  console.log("\nstate:done arrives from the pr-closed workflow in a moment.");
  return cleaned ? 0 : 1;
}

if (isEntrypoint(import.meta.url)) {
  const a = args();
  const n = a.number();
  if (!n) {
    console.error(
      "Which ticket? Usage: node automation/land.mjs <issue> [--dry-run] [--keep] [--timeout <minutes>]"
    );
    process.exit(1);
  }
  try {
    process.exit(await land(n, {
      dryRun: a.has("--dry-run"),
      keep: a.has("--keep"),
      timeoutMinutes: Number(a.get("--timeout", "20")) || 20,
    }));
  } catch (err) {
    // No label change on failure: the ticket is approved and stays approved. A
    // landing that fell over is something to retry, not a build to re-open.
    console.error("\n" + (err?.message ?? err));
    process.exit(1);
  }
}
