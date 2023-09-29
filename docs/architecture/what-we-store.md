# What We Store

Abbot stores several pieces of customer-specific information.
This document gives an overview of these pieces of information, and how we store them.

## Metadata synced from Slack

We sync metadata for organizations, users, and rooms from Slack.
Below is a non-exhaustive list of data in this category:

* Organization Metadata
    * Name
    * Domain
    * Avatar
* User Profile (Name, Avatar, etc.)
* Room/Channel Name

**We store this data in our Postgres database, `abbot-db`.**

## Abbot-originated data for organizations

We also have information that originates within Abbot.
For example, Conversation State, Settings, Skills, Lists, Aliases, etc.
Below is a non-exhaustive list of data in this category:

* Skill Metadata (**NOT** Secrets)
* Skill Packages
* Skill Code
* "Memories" (`.rem ....`)
* Abbot User profile data (Time Zone, Address, etc.)
* Role Assignments
* Conversation Metadata (Title, State, Timeline, Participants, but **NOT** message content)

**We store this data in our Postgres database, `abbot-db`.**

## Customer Secrets

We store several pieces of data we consider "customer secrets".
These are either highly sensitive customer data, or credentials that would allow a malicious internal actor to access customer data they should not have access to.
Below is a non-exhaustive list of data in this category:

* Slack/Discord API Tokens
* Skill Secrets

**We store this data in a mix of our Postgres database, `abbot-db` AND our Azure Key Vault.**
Issue https://github.com/aseriousbiz/abbot/issues/1976 is tracking work to consolidate this in Key Vault.

## Transient Processing Data

As part of processing events, we need to handle data we don't wish to store, or at least don't wish to store permenently.
For example, when we receive an event via Slack's Events API, we first store it in a Queue for later processing.
This requires we store the entire event payload, including message text, user identifiers, etc.

**We store this data persistently in our Postgres database, `abbot-db`.**

## Audit Log

We have an Audit Log that tracks all the activity in a given organization.
These events include, but are not limited to:

* Invocations of Skills
* Role changes
* Skill edits

**We store this data in our Postgres database, `abbot-db`.**
