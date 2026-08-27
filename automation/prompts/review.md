# Review PR #{{PR}} — ticket #{{NUMBER}}: {{TITLE}}

Branch `{{BRANCH}}` against `{{BASE}}`. The branch is checked out in this
worktree, so read any file in full where the diff alone lacks context — and run
the test suite rather than trusting the report below.

## The ticket this has to satisfy

{{BODY}}

## What the dev agent claims it did

Nobody watched this run. Treat the claims below as unverified.

{{DEV_REPORT}}

## Changed files

```
{{FILES}}
```

## The diff

```diff
{{DIFF}}
```

## Your task

Audit the diff against the ticket. Check that each acceptance criterion is
genuinely met and genuinely tested. Then produce your markdown review, ending
with the JSON verdict block.
