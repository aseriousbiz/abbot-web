# Handlebars Template Context

When evaluating Handlebars templates, the `InputTemplateContext` object is provided as the root context object.
Any properties on that class can be accessed via `{{ property.subProperty.etc }}`

Below is an incomplete list of template options, including some information about common trigger outputs.
For the latest information, see the `InputTemplateContext` object.

* `trigger` returns a "Step Result" object representing the result of executing the trigger, including any outputs
* `outputs` returns a merged output dictionary where the outputs from all previous steps are merged together (with more recent output names overriding older ones)
* `steps` returns a dictionary where the key is the Step ID and the value is the "Step Result" object representing the result of executing that step, including any outputs.
* `previous` returns a "Step Result" object representing the result of the step that _immediately preceeded_ the current one (returns the same thing as `trigger` for the first action step)

A Step Result has the following properties:

* `id` is the ID of the step
* `outcome` returns the outcome of the step (`Succeeded`, `Failed`, etc.)
* `outputs` is a dictionary of key-value pairs that represents the outputs of the step
* `problem` is a JSON [Problem Details](https://www.rfc-editor.org/rfc/rfc7807) object containing the error returned by this step

NOTE: Since a failed step immediately fails the entire playbook, the `problem` value is not useful in practice. This may change in the future.

## Common Outputs and their structure

* `channel`:
  * `channel.id`: The ID of the Channel
  * `channel.name`: The Name of the Channel
  * `channel.customer`: The Customer associated with this channel (same properties as a top-level `customer`)
* `customer`:
  * `customer.id`: The ID of the Customer
  * `customer.name`: The Name of the Customer
  * `customer.segments`: A list of segments the customer is in.
* `conversation`:
  * `conversation.id`: The ID of the first Slack Message in the conversation
  * `conversation.url`: The URL of the first Slack Message in the conversation
  * `conversation.title`: The title of the conversation
  * `conversation.state`: The state of the conversation
  * `conversation.channel`: The channel in which the conversation took place (same properties as top-level `channel`)
* `ticket`:
  * `type`: The type of the ticket (Zendesk, Hubspot, etc.)
  * `url`: The URL to a web view of the ticket
  * `api_url`: The URL of the API view of the ticket
  * `ticket_id`: The ID of the ticket
  * `repo`: For GitHub only, the repo in which the issue exists.
  * `owner`: For GitHub only, the owner of the repo in which the issue exists.
  * `status`: For Zendesk only, the status of the ticket
  * `thread_id`: For HubSpot only, the thread ID of the ticket
* `message`:
  * `ts`: The Slack timestamp of the message
  * `thread_ts`: The Slack timestamp of the parent message, if this is a threaded-reply
  * `text`: The text of the message
  * `url`: The URL of the message
