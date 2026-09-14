import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { CLAUDE_REVIEW_SETTINGS, claudeEnv, parseResult, runClaude } from "../lib/claude.mjs";
import { REVIEWERS, ROOT } from "../lib/core.mjs";
import { parseVerdict } from "../lib/verdict.mjs";

const SCHEMA = join(ROOT, "automation/settings/review.schema.json");
const clean = { verdict: "approve", summary: "Fine", notes: "Checked", findings: [] };
const success = (extra = {}) => JSON.stringify({ type: "result", subtype: "success", is_error: false, result: "prose", ...extra });

test("review runs headless under the read-only settings, with the prompt on stdin", () => {
  const prompt = 'Untrusted `text` $(echo secret) %PATH%\n"quotes"';
  const result = runClaude({ cwd: "C:/a folder", prompt, schema: SCHEMA }, (args, options) => {
    assert.equal(options.input, prompt);
    assert.equal(args.includes(prompt), false);
    assert.equal(args[0], "-p");
    assert.equal(args[args.indexOf("--output-format") + 1], "json");
    assert.equal(args[args.indexOf("--permission-mode") + 1], "dontAsk");
    assert.equal(args[args.indexOf("--settings") + 1], CLAUDE_REVIEW_SETTINGS);
    assert.equal(args.includes("--no-session-persistence"), true);
    assert.deepEqual(JSON.parse(args[args.indexOf("--json-schema") + 1]).required, ["verdict", "summary", "notes", "findings"]);
    return { status: 0, stdout: success({ structured_output: clean }) };
  });
  assert.equal(result.ok, true);
  assert.deepEqual(parseVerdict(result.text), clean);
});

test("nonzero exit, error results, unparseable output and timeouts cannot pass", () => {
  assert.equal(parseResult(success({ structured_output: clean }), 1).ok, false);
  assert.equal(parseResult(success({ is_error: true })).ok, false);
  assert.equal(parseResult(success({ subtype: "error_max_turns" })).ok, false);
  assert.equal(parseResult("not json").ok, false);
  const timedOut = runClaude({ cwd: ".", prompt: "x" }, () => ({ status: null, error: new Error("timeout"), stdout: success({ structured_output: clean }) }));
  assert.equal(timedOut.ok, false);
  assert.equal(timedOut.text, "timeout");
});

test("a success without structured output yields no verdict rather than a guessed one", () => {
  const result = parseResult(success());
  assert.equal(result.ok, true);
  assert.equal(parseVerdict(result.text), null);
});

test("child Claude drops the parent session and the push token but keeps other settings", () => {
  const env = claudeEnv({ CLAUDECODE: "1", CLAUDE_CODE_SESSION_ID: "x", AGENT_PUSH_OK: "1", CLAUDE_CODE_SUBAGENT_MODEL: "sonnet", PATH: "p" });
  assert.deepEqual(env, { CLAUDE_CODE_SUBAGENT_MODEL: "sonnet", PATH: "p" });
});

test("reviewer settings parse and refuse writes and GitHub access", () => {
  const settings = JSON.parse(readFileSync(CLAUDE_REVIEW_SETTINGS, "utf8"));
  for (const rule of ["Write", "Edit", "Bash(gh:*)", "Bash(git push:*)", "Bash(git commit:*)"]) {
    assert.equal(settings.permissions.deny.includes(rule), true, rule);
  }
  assert.equal(settings.permissions.allow.some((rule) => /^(Write|Edit)/.test(rule)), false);
  assert.deepEqual(REVIEWERS, ["codex", "claude"]);
});
