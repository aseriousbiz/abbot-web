# Create Zendesk Ticket Form

**Form Key**: `system.create_zendesk_ticket`

## Template Context

The template context object has the following properties:

* `Conversation` - A `ConversationTemplateModel` object representing the Conversation that the ticket is being created from.
* `Room` - A `RoomTemplateModel` object representing the Room in which the ticket is being created.
* `Organization` - An `OrganizationTemplateModel` object representing the Abbot Organization in which the ticket is being created.
* `Actor` - A `MemberTemplateModel` object representing the Member who is creating the ticket.

## Field Identifiers

See `ZendeskFormatter` for an up-to-date list.
Each field maps to a ticket property as defined [in the Zendesk API](https://developer.zendesk.com/api-reference/ticketing/tickets/tickets/#json-format).
The following field identifiers are supported:

| Field ID | Zendesk Ticket Property | Notes |
| - | - | - |
| `comment` | `comment` | The text to display in the initial comment posted with the ticket |
| `tags` | `tags` | A comma-separated list of tags to attach to the ticket (each tag has leading and trailing whitespace trimmed) |
| `type` | `type` | The type of this ticket. Allowed values are `problem`, `incident`, `question`, or `task`. |
| `priority` | `priority` | The urgency with which the ticket should be addressed. Allowed values are "urgent", "high", "normal", or "low". |
| `custom_field:[ID]` | `custom_fields` | See "Custom Fields" |

## Custom Fields

Zendesk supports custom fields which must be defined in advance.
An Abbot Form field with an ID `custom_field:123` will set the custom field with ID `123` in Zendesk.
To find the ID for a custom field, go to `https://YOUR_SUBDOMAIN.zendesk.com/admin/objects-rules/tickets/ticket-fields` and get the "Field ID" value:

<img width="915" alt="image" src="https://user-images.githubusercontent.com/7574/197075427-667e2587-4c20-422e-9926-3f8bfb51f539.png">

In the above example, an Abbot Form field with ID `custom_field:9794713875099` would set the "User-Reported Priority" custom field.

### Supported Field Types

We support the following Zendesk Custom Field Types:

* `Text` - The field value will be set as the value of the custom field.
* `Drop-down` - The field value should map to the value of an item in the custom field's options list. The field value will also be applied as a tag (this is a built-in Zendesk behavior).

Others have not yet been tested.
