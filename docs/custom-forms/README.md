# Custom Forms

Custom Forms are a staff-managed feature in Abbot.
You can manage custom forms using the "Forms" tab for an Organization in Staff Tools.
The Form Definition is a JSON-serialized `FormDefinition` class (see `Form.cs`).
For example:

```json
{
  "Version": 1,
  "Fields": [
    {
      "Id": "comment",
      "Type": "MultiLineText",
      "Title": "Description",
      "InitialValue": "{{Conversation.Title}}",
      "Options": []
    },
    {
      "Id": "custom_field:9794713875099",
      "Type": "DropdownList",
      "Title": "Priority",
      "InitialValue": "p3",
      "Required": true,
      "Options": [
        {
          "Text": "P3 - Respond within 3 business days",
          "Value": "p3"
        },
        {
          "Text": "P2 - Respond within 1 business day",
          "Value": "p2"
        },
        {
          "Text": "P1 - Respond within 2 hours",
          "Value": "p1"
        },
        {
          "Text": "P0 - Respond Immediately",
          "Value": "p0"
        }
      ]
    }
  ]
}
```

Forms are saved to the database under a specific "Key".
The key can be used by various pieces in our system to look up the form definition for a given purpose.
For example, the "Create Zendesk Ticket" modal will always use the `system.create_zendesk_ticket` form in an Organization, if any is defined.
How the form fields are interpreted depends on the specific form key.
Below are the currently defined System form keys:

* [`system.create_zendesk_ticket`](CreateZendeskTicket.md)
* [`system.create_hubspot_ticket`](CreateHubSpotTicket.md)

All Fields have the following properties, though some are ignored depending on the `Type`:

* `Id` - A implementation-defined identifier that represents how the field will be used. For example, in `system.create_zendesk_ticket` the `comment` field is used as the first comment in the ticket.
* `Type` - The type of input control to render for the field (see list of types below).
* `Title` - The title to present to the user for the form field.
* `Description` - A description which will be used as the placeholder text in the input, if that input type supports it.
* `InitialValue` - The value the field will have when the view is first rendered. This is visible to the user. This field **allows** templates.
* `DefaultValue` - The value the field will take if no value is specified for the field. This is _not_ visible to the user. This field **allows** templates.
* `Required` - A boolean indicating if the field is required. Defaults to `false`.
* `Options` - A list of options to render, used only by `DropdownList` and `RadioList`. Each option has two properties:
  * `Text` - The text to render for the option.
  * `Value` - The value to produce if this option is selected.

Fields can have one of the following types:

* `FixedValue` - No UI will be rendered for this field. It will _always_ produce the value in `InitialValue`.
* `SingleLineText` - A single-line text input will be rendered. `Options` is ignored for this field.
* `MultiLineText` - A multi-line text input will be rendered. `Options` is ignored for this field.
* `Checkbox` - A checkbox will be rendered. The value in `DefaultValue` will be returned if the checkbox is unchecked. The value `true` will be returned if the checkbox is checked. `Options` is ignored for this field.
* `RadioList` - A list of radio button will be rendered. The `InitialValue` can specify the `Value` for an `Option` to be initially selected.
* `DropdownList` - A dropdown list will be rendered. The `InitialValue` can specify the `Value` for an `Option` to be initially selected.
* `DatePicker` - A date picker will be rendered. The result will be a date in `yyyy-MM-dd` format.
* `TimePicker` - A time picker will be rendered. The result will be a time in `HH:mm` format.

The `InitialValue` and `DefaultValue` properties can use [handlebars templates](https://handlebarsjs.com). The "context" object depends on the form key.