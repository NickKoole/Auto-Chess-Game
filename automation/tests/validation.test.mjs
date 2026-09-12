import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { validate } from "../lib/validation.mjs";

test("validation preserves full failure evidence and skips dependent checks", () => {
  const cwd = mkdtempSync(join(tmpdir(), "auto-chess-validation-"));
  try {
    let count = 0;
    const result = validate(cwd, () => {
      count++;
      return { code: 1, stdout: "assertion details", stderr: "build failed" };
    }, [["", "test-build", []], ["", "test-suite", []]]);
    assert.equal(count, 1);
    assert.equal(result.ok, false);
    assert.equal(readFileSync(result.results[0].log, "utf8"), "assertion details\nbuild failed");
  } finally { rmSync(cwd, { recursive: true, force: true }); }
});

test("missing validation cannot be reported as passing", () => {
  const cwd = mkdtempSync(join(tmpdir(), "auto-chess-validation-"));
  try { assert.equal(validate(cwd, () => assert.fail(), []).ok, false); }
  finally { rmSync(cwd, { recursive: true, force: true }); }
});
