// Reconciles GitHub labels with .github/labels.yml. Idempotent.
//
// Deliberately dependency-free: parses only the exact flat shape this repo uses
// and throws on anything else rather than silently mis-parsing. Verified to
// produce identical output to a real YAML parser for this file.

import { readFileSync } from "node:fs";

const REPO = process.env.GITHUB_REPOSITORY;
const TOKEN = process.env.GITHUB_TOKEN;
if (!REPO || !TOKEN) throw new Error("GITHUB_REPOSITORY and GITHUB_TOKEN are required");

function parseLabels(text) {
  const out = [];
  let cur = null;
  text.split(/\r?\n/).forEach((raw, i) => {
    const line = raw.replace(/\s+$/, "");
    if (!line || /^\s*#/.test(line)) return;

    const item = /^- name:\s*"(.+)"$/.exec(line);
    if (item) {
      cur = { name: item[1] };
      out.push(cur);
      return;
    }
    const field = /^ {2}(color|description):\s*"(.*)"$/.exec(line);
    if (field && cur) {
      cur[field[1]] = field[2];
      return;
    }
    throw new Error(`labels.yml:${i + 1}: this parser does not support: ${JSON.stringify(raw)}`);
  });

  for (const l of out) {
    if (!l.color) throw new Error(`label "${l.name}" has no color`);
    l.description ??= "";
  }
  return out;
}

async function api(method, path, body) {
  const res = await fetch(`https://api.github.com${path}`, {
    method,
    headers: {
      authorization: `Bearer ${TOKEN}`,
      accept: "application/vnd.github+json",
      "content-type": "application/json",
      "user-agent": "auto-chess-label-sync",
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok && res.status !== 404) {
    throw new Error(`${method} ${path} -> ${res.status} ${await res.text()}`);
  }
  return { status: res.status, json: res.status === 204 ? null : await res.json().catch(() => null) };
}

const desired = parseLabels(readFileSync(".github/labels.yml", "utf8"));

const existing = new Map();
for (let page = 1; ; page++) {
  const { json } = await api("GET", `/repos/${REPO}/labels?per_page=100&page=${page}`);
  if (!Array.isArray(json) || json.length === 0) break;
  for (const l of json) existing.set(l.name, l);
  if (json.length < 100) break;
}

let created = 0;
let updated = 0;

for (const label of desired) {
  const prev = existing.get(label.name);
  if (!prev) {
    await api("POST", `/repos/${REPO}/labels`, label);
    console.log(`created  ${label.name}`);
    created++;
  } else if (prev.color !== label.color || (prev.description ?? "") !== label.description) {
    await api("PATCH", `/repos/${REPO}/labels/${encodeURIComponent(label.name)}`, {
      new_name: label.name,
      color: label.color,
      description: label.description,
    });
    console.log(`updated  ${label.name}`);
    updated++;
  }
}

// Labels are never deleted here: a stray label is harmless, but deleting one
// silently strips it from every issue that used it.
console.log(`\n${created} created, ${updated} updated, ${desired.length} desired total`);
