# Jira and documentation discipline for important work

Important work must be reflected in Jira and documentation, not only in code, chat, or CHANGELOG.

Whenever doing important work, update Jira and project documentation as part of the workflow. "Important" means any change, investigation, deployment/admin action, test phase, or project decision that a user would later expect to find in project history.

## Required behavior

1. Before starting substantial work, identify the relevant Jira project/issue if one exists.
2. If no suitable issue exists and the work is project-relevant, create or propose a Jira issue before proceeding too far.
3. Identify the relevant documentation surface (Confluence, `docs/`, runbook, changelog, or status note) and update it when the work changes behavior, process, risk, or project status.
4. When work changes scope, status, ownership, risk, or next steps, update the Jira issue labels/status/description/comment.
5. When work is completed, make Jira reflect reality: done work is `Gotowe`/`zakonczone`, future work is `Do zrobienia`/`zaplanowane`.
6. If Confluence/docs are updated for project documentation, ensure the relevant Jira links or issues also exist.
7. If Jira is updated for important work, ensure a human-readable documentation trail also exists.

## Bad

```text
User asks for a major Atlassian/project setup.
Agent creates Confluence pages and reports in chat, but leaves Jira empty/stale or leaves no durable documentation.
```

## Good

```text
User asks for a major Atlassian/project setup.
Agent creates/updates Jira epics and tickets, writes/updates the Confluence/docs trail, links Jira from documentation, verifies the Kanban board, and reports counts/statuses.
```

## Exception

Do not create Jira/documentation noise for tiny local edits, quick answers, or exploratory reads unless the user explicitly asks for tracking.
