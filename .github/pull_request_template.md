<!--
Agent-opened PRs replace this template with the run report.
This template is for PRs you open by hand.
-->

## What changed

## Acceptance criteria

<!-- Copy from the issue, tick what you actually verified. -->

## Verification

```
# server
cd server && dotnet build && dotnet test

# client
cd client && npm run typecheck && npm run test -- --run
```

## Risk / out of scope

---
Closes #
