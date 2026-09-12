import test from "node:test";
import assert from "node:assert/strict";
import { parseEvents, runCodex, runCodexInteractive, interactiveArgs } from "../lib/codex.mjs";
import { parseVerdict } from "../lib/verdict.mjs";

const stream = (...events) => events.map(JSON.stringify).join("\n");
const completed = (text = "Ready") => stream(
  { type: "thread.started", thread_id: "codex-thread" },
  { type: "item.completed", item: { type: "agent_message", text } },
  { type: "turn.completed" },
);

test("JSONL captures the assigned ID and final message, ignoring command output", () => {
  const parsed = parseEvents(stream(
    { type: "thread.started", thread_id: "assigned" },
    { type: "item.completed", item: { type: "command_execution", text: "wrong" } },
    { type: "item.completed", item: { type: "agent_message", text: "first" } },
    { type: "item.completed", item: { type: "agent_message", text: "final" } },
    { type: "turn.completed" },
  ));
  assert.deepEqual(parsed, { ok: true, sessionId: "assigned", text: "final" });
});

test("nonzero exit, failed turn, and incomplete streams cannot pass", () => {
  assert.equal(parseEvents(completed(), 1).ok, false);
  assert.equal(parseEvents('{"type":"thread.started","thread_id":"x"}').ok, false);
  assert.equal(parseEvents(completed() + '\n{"type":"turn.failed","error":{"message":"quota"}}').ok, false);
});

test("review uses stdin, schema, ephemeral read-only execution and reports timeouts", () => {
  const prompt = 'Untrusted `text` $(echo secret) %PATH%\n"quotes"';
  const result = runCodex({ cwd: "C:/a folder", prompt, schema: "C:/schema folder/review.json" }, (args, options) => {
    assert.equal(options.input, prompt);
    assert.equal(args.includes(prompt), false);
    assert.equal(args[args.indexOf("-s") + 1], "read-only");
    assert.equal(args.includes("--ephemeral"), true);
    assert.equal(args[args.indexOf("--output-schema") + 1], "C:/schema folder/review.json");
    assert.equal(args.includes('approval_policy="never"'), true);
    return { status: null, error: new Error("timeout"), stdout: completed() };
  });
  assert.equal(result.ok, false);
  assert.equal(result.text, "timeout");
});

test("new session saves Codex ID before opening interactive resume", () => {
  const order = [];
  let calls = 0;
  const result = runCodexInteractive({ cwd: ".", onSession: (id) => order.push(`saved:${id}`) }, (args) => {
    calls++;
    if (calls === 1) {
      assert.equal(args[0], "exec");
      assert.equal(args.includes("--ephemeral"), false);
      return { status: 0, stdout: completed() };
    }
    order.push("interactive");
    assert.equal(args[0], "resume");
    assert.equal(args[1], "codex-thread");
    assert.equal(args[args.indexOf("-s") + 1], "workspace-write");
    return { status: 0 };
  });
  assert.deepEqual(order, ["saved:codex-thread", "interactive"]);
  assert.equal(result.sessionId, "codex-thread");
});

test("resume uses exact ID and propagates failed interactive exit", () => {
  let calls = 0;
  const result = runCodexInteractive({ cwd: ".", sessionId: "known", onSession: () => {} }, (args) => {
    calls++;
    assert.equal(args[1], "known");
    assert.equal(args.includes("--last"), false);
    return { status: 130 };
  });
  assert.equal(calls, 1);
  assert.equal(result.code, 130);
  assert.throws(() => interactiveArgs(null));
});

test("failed initialization never opens the interactive session", () => {
  assert.throws(() => runCodexInteractive({ cwd: ".", onSession: () => assert.fail("must not save") }, () => ({ status: 1, stderr: "offline" })), /offline/);
});

test("verdict rejects malformed and inconsistent findings instead of discarding them", () => {
  const clean = { verdict: "approve", summary: "Fine", notes: "Checked", findings: [] };
  assert.deepEqual(parseVerdict(JSON.stringify(clean)), clean);
  assert.equal(parseVerdict('```json\n{}\n```'), null);
  const finding = { severity: "major", file: "x", line: 2, issue: "Broken", fix: "Fix it" };
  assert.equal(parseVerdict(JSON.stringify({ ...clean, findings: [finding] })), null);
  assert.equal(parseVerdict(JSON.stringify({ ...clean, verdict: "changes", findings: [finding] })).verdict, "changes");
  assert.equal(parseVerdict(JSON.stringify({ ...clean, findings: [{ ...finding, severity: "unknown" }] })), null);
  assert.equal(parseVerdict(JSON.stringify({ ...clean, verdict: "changes" })), null);
});


test("pipeline modules can be imported without claiming or publishing", async () => {
  await import("../dev.mjs");
  await import("../claim.mjs");
  await import("../finish.mjs");
  await import("../review.mjs");
  await import("../session.mjs");
});
