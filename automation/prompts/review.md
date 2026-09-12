# Review PR #{{PR}} — ticket #{{NUMBER}}: {{TITLE}}

Branch `{{BRANCH}}` against `{{BASE}}`. The branch is checked out in this
worktree, so read any file in full where the diff alone lacks context — and inspect the independent validation logs rather than trusting the report below.

## Scope of this pass

{{SCOPE}}

## The ticket this has to satisfy

{{BODY}}

## What the dev agent claims it did

A human steered this session, so its shape was seen as it happened — but not
line by line. Treat the claims below as unverified.

{{DEV_REPORT}}

## Runner validation

{{VALIDATION}}

Read these logs. They are evidence from the runner, not claims from the author.

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
genuinely met and genuinely tested. Then return the JSON review required by the output schema.
