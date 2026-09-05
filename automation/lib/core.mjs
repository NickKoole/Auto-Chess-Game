// Shared plumbing for the local agents: gh CLI, git worktrees, and both ways of
// running Claude — interactive for the dev session, headless for the reviewer.
// Zero dependencies by design — this must run from a bare checkout.

import { spawnSync } from "node:child_process";
import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { createInterface } from "node:readline/promises";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { randomUUID } from "node:crypto";

const IS_WIN = process.platform === "win32";

export const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
export const STATE_DIR = join(ROOT, ".agent-state");
export const PROMPTS = join(ROOT, "automation", "prompts");
export const DEV_SETTINGS = join(ROOT, "automation", "settings", "dev.json");
// Scratch space inside a worktree: the task going in, the report coming out.
// Gitignored, so the commit sweep never picks it up.
export const AGENT_DIR = ".agent";
// Derived from the checkout directory rather than hardcoded, so renaming the
// repo or the folder cannot silently point worktrees at a path that no longer
// matches the branch they belong to.
export const WORKTREE_ROOT = resolve(ROOT, "..", `${basename(ROOT)}-work`);

// One of these sits on an issue at any time. `needs-you` is the one that matters:
// it means the machine is done and the ball is in your court.
export const STATES = [
  "state:backlog",
  "state:ready",
  "state:building",
  "state:reviewing",
  "state:needs-you",
  "state:approved",
  "state:done",
  "state:blocked",
];

// Agents write comments with these markers so a later run can tell its own
// output from your feedback.
export const MARK_DEV = "<!-- agent:dev -->";
export const MARK_REVIEW = "<!-- agent:review -->";

// --- process -----------------------------------------------------------------

// With shell:true (needed on Windows for .cmd shims) Node does NOT escape argv,
// so anything containing a space must be quoted before it gets joined.
const quote = (a) => (IS_WIN && /[\s&|<>^"]/.test(a) ? `"${a.replace(/"/g, '""')}"` : a);

export function run(cmd, args, opts = {}) {
  const res = spawnSync(cmd, IS_WIN ? args.map(quote) : args, {
    encoding: "utf8",
    maxBuffer: 64 * 1024 * 1024,
    shell: IS_WIN,
    ...opts,
  });
  if (res.error) throw res.error;
  return {
    code: res.status ?? 1,
    stdout: (res.stdout || "").trim(),
    stderr: (res.stderr || "").trim(),
    timedOut: res.signal === "SIGTERM" && opts.timeout != null,
  };
}

export function must(cmd, args, opts = {}) {
  const r = run(cmd, args, opts);
  if (r.code !== 0) {
    throw new Error(`${cmd} ${args.join(" ")} failed (exit ${r.code})\n${r.stderr || r.stdout}`);
  }
  return r.stdout;
}

// --- github ------------------------------------------------------------------

export const gh = (args, opts) => must("gh", args, opts);
export const ghJson = (args, opts) => JSON.parse(gh(args, opts) || "null");

export function assertGh() {
  if (run("gh", ["auth", "status"]).code !== 0) {
    throw new Error("gh is not authenticated. Run:  gh auth login");
  }
  if (run("git", ["rev-parse", "--git-dir"], { cwd: ROOT }).code !== 0) {
    throw new Error(`${ROOT} is not a git repository.`);
  }
}

export const listByState = (state) =>
  ghJson([
    "issue", "list",
    "--label", state,
    "--state", "open",
    "--limit", "50",
    "--json", "number,title,labels,updatedAt",
  ]) ?? [];

export const getIssue = (n) =>
  ghJson([
    "issue", "view", String(n),
    "--json", "number,title,body,labels,state,url,comments",
  ]);

export const labelNames = (issue) => issue.labels.map((l) => l.name ?? l);
export const stateOf = (issue) => labelNames(issue).find((l) => STATES.includes(l)) ?? null;
export const areaOf = (issue) =>
  (labelNames(issue).find((l) => l.startsWith("area:")) ?? "area:sim").slice(5);

/**
 * The ticket to work on: the number you passed, or a pick from the ready pool.
 * Epics are filtered out of the menu because nothing builds one directly.
 */
export async function chooseIssue(explicit) {
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

export function setState(n, next) {
  if (!STATES.includes(next)) throw new Error(`unknown state: ${next}`);
  const stale = labelNames(getIssue(n)).filter((l) => STATES.includes(l) && l !== next);
  const args = ["issue", "edit", String(n), "--add-label", next];
  for (const s of stale) args.push("--remove-label", s);
  gh(args);
  return next;
}

export const comment = (n, body) =>
  gh(["issue", "comment", String(n), "--body-file", "-"], { input: body });

export const prComment = (pr, body) =>
  gh(["pr", "comment", String(pr), "--body-file", "-"], { input: body });

/**
 * gh uses different timestamp keys across comment and review payloads
 * (createdAt vs submittedAt). Accept whichever is present rather than
 * silently treating every item as brand new.
 */
export const timeOf = (o) =>
  Date.parse(o?.createdAt ?? o?.submittedAt ?? o?.updatedAt ?? "") || 0;

// --- session store -----------------------------------------------------------

const sessionPath = (n) => join(STATE_DIR, `issue-${n}.json`);

export function loadSession(n) {
  try {
    return JSON.parse(readFileSync(sessionPath(n), "utf8"));
  } catch {
    return null;
  }
}

export function saveSession(n, patch) {
  mkdirSync(STATE_DIR, { recursive: true });
  const prev = loadSession(n) ?? { issue: n, sessionId: randomUUID(), runs: 0 };
  const next = { ...prev, ...patch, updatedAt: new Date().toISOString() };
  writeFileSync(sessionPath(n), JSON.stringify(next, null, 2));
  return next;
}

export const clearSession = (n) => rmSync(sessionPath(n), { force: true });

// --- git / worktrees ---------------------------------------------------------

export const slug = (s) =>
  s.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "").slice(0, 40) || "ticket";

export function defaultBranch() {
  const r = run("git", ["symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD"], { cwd: ROOT });
  if (r.code === 0 && r.stdout) return r.stdout.replace(/^origin\//, "");
  for (const guess of ["main", "master"]) {
    if (run("git", ["ls-remote", "--exit-code", "--heads", "origin", guess], { cwd: ROOT }).code === 0) {
      return guess;
    }
  }
  return "main";
}

/** Create (or reuse) an isolated checkout for this ticket. */
export function ensureWorktree(issue) {
  // Prefer the branch a prior claim already recorded: the issue title can be
  // renamed after claiming (e.g. adding epic numbering), and recomputing the
  // slug then would point at a branch that was never created.
  const branch = loadSession(issue.number)?.branch ?? `agent/${issue.number}-${slug(issue.title)}`;
  const dir = join(WORKTREE_ROOT, `issue-${issue.number}`);
  const base = defaultBranch();

  mkdirSync(WORKTREE_ROOT, { recursive: true });
  must("git", ["fetch", "origin", base], { cwd: ROOT });

  if (existsSync(join(dir, ".git"))) {
    must("git", ["checkout", branch], { cwd: dir });
    return { dir, branch, base, fresh: false };
  }

  // Pick up a branch a previous run already pushed.
  if (run("git", ["ls-remote", "--exit-code", "--heads", "origin", branch], { cwd: ROOT }).code === 0) {
    must("git", ["fetch", "origin", branch], { cwd: ROOT });
    must("git", ["worktree", "add", dir, "-B", branch, `origin/${branch}`], { cwd: ROOT });
    return { dir, branch, base, fresh: false };
  }

  must("git", ["worktree", "add", dir, "-b", branch, `origin/${base}`], { cwd: ROOT });
  return { dir, branch, base, fresh: true };
}

export const isDirty = (cwd) => run("git", ["status", "--porcelain"], { cwd }).stdout.length > 0;

export function commitsAhead(cwd, base) {
  const r = run("git", ["rev-list", "--count", `origin/${base}..HEAD`], { cwd });
  return r.code === 0 ? Number(r.stdout) || 0 : 0;
}

/** Env that opens the push guard. Only finish.mjs sets it. */
export const PUSH_ENV = { ...process.env, AGENT_PUSH_OK: "1" };

/**
 * Refuse any push out of this worktree that did not come from finish.mjs.
 *
 * The permission file already denies the dev agent `git push`, but that only
 * binds a session someone launched with `--settings dev.json`. This binds the
 * worktree itself, so it holds whatever opened the session — dev.mjs, an Orca
 * terminal, or you typing `claude` by hand.
 *
 * Worktrees share `.git/hooks` with the main checkout, so a plain hook here
 * would also block your own pushes from the main tree. `extensions.worktreeConfig`
 * plus a per-worktree `core.hooksPath` keeps it contained to this checkout.
 */
export function installPushGuard(dir) {
  const hooks = join(dir, AGENT_DIR, "hooks");
  mkdirSync(hooks, { recursive: true });

  const hook = join(hooks, "pre-push");
  writeFileSync(hook, [
    "#!/bin/sh",
    "# Written by automation/claim.mjs. Publishing goes through automation/finish.mjs,",
    "# which sets AGENT_PUSH_OK. Delete the worktree to be rid of this.",
    '[ "$AGENT_PUSH_OK" = "1" ] && exit 0',
    'echo "refusing push: this worktree publishes through automation/finish.mjs" >&2',
    "exit 1",
    "",
  ].join("\n"));
  chmodSync(hook, 0o755);

  must("git", ["config", "extensions.worktreeConfig", "true"], { cwd: dir });
  must("git", ["config", "--worktree", "core.hooksPath", hooks], { cwd: dir });
}

// --- claude ------------------------------------------------------------------

/**
 * Run Claude Code headless.
 *
 * The prompt goes in on stdin so it never has to survive shell quoting, and
 * permissions come from a checked-in settings file rather than a flag soup —
 * which also means the deny list is reviewable in git. The model is pinned in
 * that same file. `fallbackModel` is the one thing that stays a flag — it is
 * not a property of the run, only of what to do when the pinned model is
 * unavailable, and this run has nobody sitting on it to retry by hand.
 */
export function runClaude({
  cwd, prompt, settings, systemPromptFile, sessionId, resume, fallbackModel,
  timeoutMs = 45 * 60_000,
}) {
  const args = ["-p", "--output-format", "json", "--permission-mode", "acceptEdits"];
  if (settings) args.push("--settings", settings);
  if (fallbackModel) args.push("--fallback-model", fallbackModel);
  if (systemPromptFile) args.push("--append-system-prompt-file", systemPromptFile);
  if (resume) args.push("--resume", resume);
  else if (sessionId) args.push("--session-id", sessionId);

  const started = Date.now();
  const res = run("claude", args, { cwd, input: prompt, timeout: timeoutMs });
  const seconds = Math.round((Date.now() - started) / 1000);

  if (res.timedOut) {
    return { ok: false, text: `Timed out after ${Math.round(timeoutMs / 60000)} minutes.`, sessionId: sessionId ?? resume, seconds };
  }

  let parsed = null;
  try {
    parsed = JSON.parse(res.stdout);
  } catch { /* fall through */ }

  if (!parsed) {
    return {
      ok: res.code === 0,
      text: res.stdout || res.stderr || "(no output)",
      sessionId: sessionId ?? resume ?? null,
      seconds,
    };
  }

  return {
    ok: res.code === 0 && parsed.is_error !== true,
    text: parsed.result ?? "(no result field in Claude output)",
    sessionId: parsed.session_id ?? sessionId ?? resume ?? null,
    cost: parsed.total_cost_usd ?? null,
    turns: parsed.num_turns ?? null,
    seconds,
  };
}

/**
 * The argv for an attached dev session. Shared so that the line claim.mjs
 * prints for you to run by hand is the same one dev.mjs runs itself — a
 * printed command that quietly drops the system prompt or the permission mode
 * would be worse than none at all.
 */
export function interactiveArgs({ initialPrompt, settings, systemPromptFile, sessionId, resume }) {
  const args = ["--permission-mode", "acceptEdits"];
  if (settings) args.push("--settings", settings);
  if (systemPromptFile) args.push("--append-system-prompt-file", systemPromptFile);
  if (resume) args.push("--resume", resume);
  else if (sessionId) args.push("--session-id", sessionId);
  if (initialPrompt) args.push(initialPrompt);
  return args;
}

/** The same session, as something you can paste into a terminal. */
export const interactiveCommand = (opts) =>
  ["claude", ...interactiveArgs(opts).map((a) => (/[\s&|<>^"]/.test(a) ? `"${a.replace(/"/g, '\\"')}"` : a))].join(" ");

/**
 * Claude attached to your terminal, for the dev run. There is no captured output
 * to parse here — you are the one reading it — so the agent leaves its report in
 * a file and the runner picks that up after you exit. No timeout either: the
 * session lasts as long as you sit with it.
 */
export function runClaudeInteractive(opts) {
  const args = interactiveArgs(opts);
  const { cwd, sessionId, resume } = opts;

  const started = Date.now();
  const res = spawnSync("claude", IS_WIN ? args.map(quote) : args, {
    cwd,
    stdio: "inherit",
    shell: IS_WIN,
  });
  if (res.error) throw res.error;

  return {
    code: res.status ?? 1,
    sessionId: resume ?? sessionId ?? null,
    seconds: Math.round((Date.now() - started) / 1000),
  };
}

export function template(file, vars) {
  let text = readFileSync(file, "utf8");
  for (const [k, v] of Object.entries(vars)) text = text.replaceAll(`{{${k}}}`, v ?? "");
  return text;
}

// --- cli helpers -------------------------------------------------------------

export function args(argv = process.argv.slice(2)) {
  const flags = new Set(argv.filter((a) => a.startsWith("--")));
  return {
    has: (f) => flags.has(f),
    get: (f, fallback = null) => {
      const i = argv.indexOf(f);
      return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith("--") ? argv[i + 1] : fallback;
    },
    number: () => {
      const n = argv.find((a) => /^\d+$/.test(a));
      return n ? Number(n) : null;
    },
  };
}

/**
 * True when this module is the file node was asked to run, rather than an
 * import. claim.mjs and finish.mjs are both a CLI and a library — dev.mjs
 * imports them so there is exactly one copy of each half.
 */
export function isEntrypoint(metaUrl) {
  if (!process.argv[1]) return false;
  try {
    return realpathSync(fileURLToPath(metaUrl)) === realpathSync(process.argv[1]);
  } catch {
    return false;
  }
}

export const fmtDuration = (s) => (s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`);
