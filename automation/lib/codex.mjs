import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { delimiter, join } from "node:path";
import { ROOT } from "./core.mjs";

export function codexExecutable(env = process.env, platform = process.platform) {
  if (env.AUTO_CHESS_CODEX_BIN) return { command: env.AUTO_CHESS_CODEX_BIN, prefix: [] };
  if (platform !== "win32") return { command: "codex", prefix: [] };
  for (const dir of (env.PATH ?? env.Path ?? "").split(delimiter)) {
    const exe = join(dir, "codex.exe");
    if (existsSync(exe)) return { command: exe, prefix: [] };
    const js = join(dir, "node_modules", "@openai", "codex", "bin", "codex.js");
    if (existsSync(js)) return { command: process.execPath, prefix: [js] };
  }
  throw new Error("Codex CLI not found. Install it or set AUTO_CHESS_CODEX_BIN to the native executable.");
}

export function invokeCodex(args, opts = {}) {
  const { command, prefix } = codexExecutable();
  const env = { ...process.env };
  delete env.AGENT_PUSH_OK;
  return spawnSync(command, [...prefix, ...args], { encoding: "utf8", maxBuffer: 64 * 1024 * 1024, ...opts, env, shell: false });
}

export function configArgs(role, settings = JSON.parse(readFileSync(join(ROOT, "automation/settings/codex.json"), "utf8"))) {
  const instructions = role === "bootstrap"
    ? "This turn only initializes a saved thread. Reply Ready, without using tools or starting work."
    : `Read and follow ${join(ROOT, "AGENTS.md").replaceAll("\\", "/")} and ${join(ROOT, `automation/prompts/${role}-system.md`).replaceAll("\\", "/")} before doing any work.`;
  return ["-m", settings.model, "-c", `model_reasoning_effort=${JSON.stringify(settings.reasoningEffort)}`,
    "-c", `developer_instructions=${JSON.stringify(instructions)}`,
    "-c", 'web_search="disabled"', "-c", "sandbox_workspace_write.network_access=false"];
}

export function parseEvents(stdout, code = 0) {
  let sessionId = null;
  let text = "";
  let completed = false;
  const errors = [];
  for (const line of stdout.split(/\r?\n/).filter(Boolean)) {
    let event;
    try { event = JSON.parse(line); } catch { continue; }
    if (event.type === "thread.started") sessionId = event.thread_id;
    if (event.type === "item.completed" && event.item?.type === "agent_message") text = event.item.text;
    if (event.type === "turn.completed") completed = true;
    if (event.type === "turn.failed" || event.type === "error") errors.push(event.error?.message ?? event.message ?? "Codex failed");
  }
  return { ok: code === 0 && completed && errors.length === 0, sessionId, text: errors.length ? errors.join("\n") : text };
}

export function runCodex({ cwd, prompt, role = "review", schema, timeoutMs = 30 * 60_000 }, invoke = invokeCodex) {
  const args = ["exec", ...configArgs(role), "-s", "read-only", "-c", 'approval_policy="never"', "--json"];
  if (role === "review") args.push("--ephemeral");
  if (schema) args.push("--output-schema", schema);
  args.push("-");
  const started = Date.now();
  const result = invoke(args, { cwd, input: prompt, timeout: timeoutMs });
  const parsed = parseEvents(result.stdout ?? "", result.status ?? 1);
  if (result.error) {
    parsed.ok = false;
    parsed.text = result.error.message;
  }
  if (!parsed.ok && !parsed.text) parsed.text = result.stderr || "Codex returned no completed turn.";
  return { ...parsed, seconds: Math.round((Date.now() - started) / 1000) };
}

export function interactiveArgs(sessionId) {
  if (!sessionId) throw new Error("A recorded Codex session ID is required to resume.");
  return ["resume", sessionId, ...configArgs("dev"), "-s", "workspace-write", "-a", "on-request",
    "Read .agent/task.md and follow it. Write .agent/report.md before finishing."];
}

export function runCodexInteractive({ cwd, sessionId, onSession }, invoke = invokeCodex) {
  if (!sessionId) {
    const started = runCodex({ cwd, prompt: "Reply Ready only. Do not use tools.", role: "bootstrap" }, invoke);
    if (!started.ok || !started.sessionId) throw new Error(`Could not initialize Codex: ${started.text}`);
    sessionId = started.sessionId;
  }
  onSession(sessionId);
  const started = Date.now();
  const result = invoke(interactiveArgs(sessionId), { cwd, stdio: "inherit" });
  if (result.error) throw result.error;
  return { code: result.status ?? 1, sessionId, seconds: Math.round((Date.now() - started) / 1000) };
}
