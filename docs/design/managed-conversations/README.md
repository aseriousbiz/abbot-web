# Abbot Managed Conversations

## The State Machine

<img width="803" alt="image" src="https://user-images.githubusercontent.com/7574/152226761-4abcd876-7c95-4fb9-a207-105de64d07cd.png">

<details>
<summary>Mermaid Source</summary>

```mermaid
stateDiagram-v2
    [*] --> New: message from foreign org user
    New --> Waiting: message from home org user
    Waiting --> NeedsResponse: message from foreign org user
    NeedsResponse --> Waiting: message from home org user
    Waiting --> Closed: closed
    NeedsResponse --> Closed: closed
    Closed --> Waiting: reopened by home org user
    Closed --> NeedsResponse: message from foreign org user
    Closed --> Archived: archived
    Archived --> Closed: unarchive
```

</details>
