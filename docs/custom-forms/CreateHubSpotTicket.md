# Create HubSpot Ticket Form

**Form Key**: `system.create_hubspot_ticket`

## Template Context

The template context object has the following properties:

* `Conversation` - A `ConversationTemplateModel` object representing the Conversation that the ticket is being created from.
* `Room` - A `RoomTemplateModel` object representing the Room in which the ticket is being created.
* `Organization` - An `OrganizationTemplateModel` object representing the Abbot Organization in which the ticket is being created.
* `Actor` - A `MemberTemplateModel` object representing the Member who is creating the ticket.

## Field Identifiers

HubSpot tickets are created with a simple JSON property bag:

```json
{
    "properties": {
        "subject": "Subject",
        "hs_ticket_priority": "LOW",
        ...
    }
}
```

All properties are treated equally, whether they are built-in (like `hs_ticket_priority`) or custom.
The field IDs in the `system.create_hubspot_ticket` form should map to the "Internal Name" of the HubSpot ticket property to set.
When calling the API to create the ticket, we take the form result dictionary, add the `hs_pipeline` and `hs_pipeline_stage` properties from the organization's HubSpot settings, and use that as the `properties` dictionary.

### Supported Field Types

We support the following HubSpot Custom Field Types:

* `Single-line Text` - The field value will be set as the value of the custom field.
* `Multi-line Text` - The field value will be set as the value of the custom field.
* `Dropdown select` - The field value should map to the value of an item in the custom field's options list.

Others have not yet been tested.
