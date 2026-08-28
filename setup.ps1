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

# PowerShell 5.1 wraps a native command's stderr in an ErrorRecord whenever the
# stream is redirected, and $ErrorActionPreference = "Stop" then promotes that to
# a terminating error — so an ordinary non-zero exit becomes a stack trace before
# we can turn it into a readable message. Run redirected native calls through
# here, and test $LASTEXITCODE rather than $?, which the same wrapping corrupts.
function Quiet([scriptblock]$Block) {
  $prev = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try { & $Block } finally { $ErrorActionPreference = $prev }
}

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

Quiet { gh auth status *> $null }
if ($LASTEXITCODE -ne 0) { throw "gh is not authenticated. Run:  gh auth login" }
Ok "gh authenticated"

# ------------------------------------------------------------------- repo ----
Step "Repository"
# `git remote` prints nothing and exits 0 when there are no remotes, so this
# asks the question without provoking an error to swallow in the first place.
if ((git remote) -notcontains "origin") {
  $me = (gh api user --jq .login)
  Note "Creating $me/$RepoName ($Visibility)"
  gh repo create "$RepoName" "--$Visibility" --source=. --remote=origin
  if ($LASTEXITCODE -ne 0) { throw "gh repo create failed" }
  Ok "created and linked"
} else {
  Ok "origin already set: $(git remote get-url origin)"
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
if ($LASTEXITCODE -ne 0) { throw "git push failed" }
Ok "pushed to origin/main"

$nwo = (gh repo view --json nameWithOwner --jq .nameWithOwner)

# ----------------------------------------------------------------- labels ----
Step "Labels"
$env:GITHUB_TOKEN = (gh auth token)
$env:GITHUB_REPOSITORY = $nwo
node .github/scripts/sync-labels.mjs
if ($LASTEXITCODE -ne 0) { throw "label sync failed" }

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
