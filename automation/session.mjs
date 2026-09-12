import { args, isEntrypoint, loadSession, saveSession } from "./lib/core.mjs";
import { runCodexInteractive } from "./lib/codex.mjs";

export function runTicketSession(n) {
  const session = loadSession(n);
  if (!session?.dir) throw new Error(`Claim ticket #${n} first.`);
  return runCodexInteractive({
    cwd: session.dir,
    sessionId: session.codexSessionId ?? null,
    onSession: (codexSessionId) => saveSession(n, { codexSessionId }),
  });
}

if (isEntrypoint(import.meta.url)) {
  try {
    const n = args().number();
    if (!n) throw new Error("Usage: node automation/session.mjs <issue-number>");
    process.exit(runTicketSession(n).code);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
}
