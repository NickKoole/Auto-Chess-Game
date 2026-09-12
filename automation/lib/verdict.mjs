export function parseVerdict(text) {
  try {
    const value = JSON.parse(text);
    if (!["approve", "changes"].includes(value.verdict) || typeof value.summary !== "string" || typeof value.notes !== "string" || !Array.isArray(value.findings)) return null;
    for (const f of value.findings) {
      if (!f || !["blocker", "major", "minor", "nit"].includes(f.severity) || typeof f.issue !== "string" || !f.issue.trim() ||
          typeof f.file !== "string" || typeof f.fix !== "string" || !(f.line === null || Number.isInteger(f.line) && f.line > 0)) return null;
    }
    const blocking = value.findings.some((f) => ["blocker", "major"].includes(f.severity));
    if ((value.verdict === "changes") !== blocking) return null;
    return value;
  } catch { return null; }
}
