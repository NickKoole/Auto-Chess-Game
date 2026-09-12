import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { run } from "./core.mjs";

export function validationPlan(cwd) {
  const checks = [];
  if (existsSync(join(cwd, "server/AutoChess.slnx"))) {
    checks.push(["server", "dotnet", ["build"]], ["server", "dotnet", ["test", "--no-build"]]);
  }
  if (existsSync(join(cwd, "client/package-lock.json"))) {
    checks.push(["client", "npm", ["ci"]], ["client", "npm", ["run", "typecheck"]],
      ["client", "npm", ["run", "test", "--", "--run"]], ["client", "npm", ["run", "build"]]);
  }
  if (existsSync(join(cwd, "automation/tests"))) checks.push(["", "node", ["--test", "automation/tests/*.test.mjs"]]);
  return checks;
}

export function validate(cwd, execute = run, plan = validationPlan(cwd)) {
  const results = [];
  const logs = join(cwd, ".agent", "validation");
  mkdirSync(logs, { recursive: true });
  for (const [dir, command, args] of plan) {
    console.log(`checking ${dir || "."}: ${command} ${args.join(" ")}`);
    let result;
    try { result = execute(command, args, { cwd: join(cwd, dir), timeout: 15 * 60_000 }); }
    catch (error) { result = { code: 1, stderr: error.message, stdout: "" }; }
    const log = join(logs, `${results.length + 1}.txt`);
    writeFileSync(log, `${result.stdout ?? ""}\n${result.stderr ?? ""}`);
    results.push({ command: `${dir || "."}: ${command} ${args.join(" ")}`, code: result.code, log });
    if (result.code !== 0) break;
  }
  return { ok: results.length > 0 && results.every((r) => r.code === 0), results };
}

export function validationSummary(validation) {
  return validation.results.map((r) => `${r.command}: exit ${r.code}; full output: ${r.log}`).join("\n") || "No validation commands were available.";
}
