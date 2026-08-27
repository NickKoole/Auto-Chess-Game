<#
.SYNOPSIS
  One-time bootstrap. Idempotent — safe to re-run.

.DESCRIPTION
  Creates the GitHub repo, pushes, and reconciles the labels. That's all the
  setup there is: no board, no tokens, no background services.

.EXAMPLE
  ./setup.ps1
  ./setup.ps1 -Visibility public
#>
[CmdletBinding()]
param(
  [string]$RepoName = "Idle-Auto-Chess-Game",
  [ValidateSet("private", "public")]
  [string]$Visibility = "private"
)

$ErrorActionPreference = "Stop"

function Step($m) { Write-Host "`n=== $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  [ok] $m" -ForegroundColor Green }
function Note($m) { Write-Host "  [--] $m" -ForegroundColor DarkGray }

# ---------------------------------------------------------------- preflight --
Step "Preflight"
foreach ($t in @("gh", "git", "node", "dotnet", "claude")) {
  if (-not (Get-Command $t -ErrorAction SilentlyContinue)) { throw "$t is not on PATH." }
  Ok "$t"
}

$sdks = (dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '(?m)^10.') {
  throw "No .NET 10 SDK found. The repo targets net10.0 (see CLAUDE.md).`n" +
        "  Install it with:  winget install Microsoft.DotNet.SDK.10`n" +
        "  Installed now:`n$sdks"
}
Ok ".NET 10 SDK present"

gh auth status *> $null
if (-not $?) { throw "gh is not authenticated. Run:  gh auth login" }
Ok "gh authenticated"

# ------------------------------------------------------------------- repo ----
Step "Repository"
$remote = (git remote get-url origin 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $remote) {
  $me = (gh api user --jq .login)
  Note "Creating $me/$RepoName ($Visibility)"
  gh repo create "$RepoName" "--$Visibility" --source=. --remote=origin
  if (-not $?) { throw "gh repo create failed" }
  Ok "created and linked"
} else {
  Ok "origin already set: $remote"
}

$branch = (git rev-parse --abbrev-ref HEAD)
if ($branch -ne "main") {
  Note "Renaming '$branch' to 'main' (the workflows target main)"
  git branch -M main
}

git add -A
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
  git commit -m "chore(infra): ticket pipeline and local agents"
  Ok "committed"
} else {
  Ok "nothing to commit"
}

git push -u origin main
if (-not $?) { throw "git push failed" }
Ok "pushed to origin/main"

$nwo = (gh repo view --json nameWithOwner --jq .nameWithOwner)

# ----------------------------------------------------------------- labels ----
Step "Labels"
$env:GITHUB_TOKEN = (gh auth token)
$env:GITHUB_REPOSITORY = $nwo
node .github/scripts/sync-labels.mjs
if (-not $?) { throw "label sync failed" }

# ------------------------------------------------------------------- done ----
Step "Done"
Write-Host @"
  Repo: https://github.com/$nwo

  File your first ticket:

      gh issue create --repo $nwo

  Groom it, label it state:ready, then build it:

      node automation/dev.mjs

  Nothing runs in the background. Every run starts with you.

"@ -ForegroundColor White
