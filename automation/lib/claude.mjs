import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { ROOT } from "./core.mjs";

export const CLAUDE_REVIEW_SETTINGS = join(ROOT, "automation/settings/claude-review.json");
// Only where to land when the pinned model is overloaded: an unattended review has nobody to retry it.
const FALLBACK_MODEL = "claude-haiku-4-5-20251001";

// Set by a parent Claude Code session; a child that inherits them refuses to start or attaches to the parent.
const PARENT_SESSION_ENV = [
  "CLAUDECODE", "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_SESSION_ID", "CLAUDE_CODE_CHILD_SESSION",
  "CLAUDE_CODE_SESSION_ATTENDED", "CLAUDE_CODE_MESSAGING_SOCKET", "CLAUDE_CODE_MESSAGING_TOKEN",
  "CLAUDE_CODE_BRIDGE_SESSION_ID", "CLAUDE_CODE_EXECPATH", "CLAUDE_PID",
];

export function claudeEnv(env = process.env) {
  const next = { ...env };
  for (const key of ["AGENT_PUSH_OK", ...PARENT_SESSION_ENV]) delete next[key];
  return next;
}

export function invokeClaude(args, opts = {}) {
  const command = process.env.AUTO_CHESS_CLAUDE_BIN || "claude";
  return spawnSync(command, args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024, ...opts, env: claudeEnv(), shell: false });
}

export function reviewArgs({
  settings = CLAUDE_REVIEW_SETTINGS,
  systemPromptFile = join(ROOT, "automation/prompts/review-system.md"),
  schema,
} = {}) {
  const args = ["-p", "--output-format", "json", "--no-session-persistence", "--permission-mode", "dontAsk",
    "--settings", settings, "--fallback-model", FALLBACK_MODEL, "--append-system-prompt-file", systemPromptFile];
  if (schema) args.push("--json-schema", JSON.stringify(JSON.parse(readFileSync(schema, "utf8"))));
  return args;
}

export function parseResult(stdout, code = 0) {
  let parsed;
  try { parsed = JSON.parse(stdout); } catch { return { ok: false, text: stdout || "Claude returned no JSON result." }; }
  const ok = code === 0 && parsed.is_error !== true && parsed.subtype === "success";
  const text = ok && parsed.structured_output ? JSON.stringify(parsed.structured_output) : String(parsed.result ?? "");
  return { ok, text: text || `Claude run ended with ${parsed.subtype ?? "no result"}.`, cost: parsed.total_cost_usd ?? null };
}

export function runClaude({ cwd, prompt, schema, timeoutMs = 30 * 60_000 }, invoke = invokeClaude) {
  const started = Date.now();
  const result = invoke(reviewArgs({ schema }), { cwd, input: prompt, timeout: timeoutMs });
  const parsed = parseResult(result.stdout ?? "", result.status ?? 1);
  if (result.error) {
    parsed.ok = false;
    parsed.text = result.error.message;
  }
  if (!parsed.ok && !parsed.text) parsed.text = result.stderr || "Claude returned no result.";
  return { ...parsed, seconds: Math.round((Date.now() - started) / 1000) };
}
