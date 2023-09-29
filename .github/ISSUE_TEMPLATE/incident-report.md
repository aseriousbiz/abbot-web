---
name: Incident Report
about: Reports an incident that is occurring in production
title: 'Incident Report [Date]: [Description]'
labels: incident-report
assignees: ''

---

## What was down, and for how long

*Provide a brief description of what went wrong, and how long we were out for.*

## What Happened?

*Describe what happened, and include any helpful Kusto queries*

<details>
<summary>Query</summary>

*Insert any useful Kusto Queries here. Put the text of the query, with a time filter in the query itself to show the real data observed during the incident. Get a link to the query by clicking "Share" on the Kusto query window. Include both the query text, in a code fence, and the link.*

</details>

<details>
<summary>Full Dataset</summary>

*Insert any useful Kusto query data here. Export it as a CSV and use a CSV-Markdown converter (run locally, to avoid sending data elsewhere) to create a table*

</details>

*Provide summaries of relevant data from Kusto*

## How did we respond?

*Provide a rough timeline of how we responded, link to Slack threads and PRs as necessary*

## 5 Why's

*Keep asking "Why" until you reach the root cause. Not limited to 5, it's just a handy name and you should expect to get to around 5 "Why" questions: Why did X happen? Because Y. Why did Y happen? Because Z. etc.*

## Proposed Action Items

*Write some proposed action items to prevent this issue in the future. Link to any issues created.*
