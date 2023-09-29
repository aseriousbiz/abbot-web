---
Title: Reference
---

# Abbot Reference Documentation

## The Bot Object
* `Bot.Id` <sup>C#</sup> `bot.id` <sup>Python, JavaScript</sup>
    - The identifier for the Bot. This can be used to test if a user has mentioned the bot (by comparing Mention.Id and Bot.Id for equality).
* `Bot.Arguments` <sup>C#</sup> `bot.arguments` <sup>Python, JavaScript</sup> `args` <sup>Python</sup>
    - The text that the user entered when calling the skill. This does not include the name of the skill. In C#, this will be a special collection of parsed arguments. Access the `Value` property to get the full text as a string.
* `Bot.IsChat` <sup>C#</sup> `bot.is_chat` <sup>Python</sup> `bot.isChat` <sup>JavaScript</sup>
   - Set to true if the message came from chat or the Bot Console. False if it came from a trigger.
* `Bot.IsRequest` <sup>C#</sup> `bot.is_request` <sup>Python</sup> `bot.isRequest` <sup>JavaScript</sup>
   - True if the message comes from either a Scheduled Trigger or an HTTP Trigger. Otherwise, set to false.
* `Bot.Brain` <sup>C#</sup> `bot.brain` <sup>Python, JavaScript</sup>
    - The interface to Abbot's storage system. See `Managing Data` section for more.
* `Bot.Code` <sup>C#</sup> `bot.code` <sup>Python, JavaScript</sup>
    - The code of the skill. This is used for running the skill and should be considered an Internal variable. Be careful with this!
* `Bot.From` <sup>C#</sup> `bot.from_user` <sup>Python</sup> `bot.from` <sup>JavaScript</sup>
  - The `Id` and `Name` of the user making the request. Only populated when `Bot.IsChat` is true.
* `Bot.Mentions` <sup>C#</sup> `bot.mentions` <sup>Python, JavaScript</sup>
    - An array of `Mention` objects extracted from the entered text.
* `Bot.Request` <sup>C#</sup> `bot.request` <sup>Python, JavaScript</sup>
   - Information about the request sent to the bot, when the bot is activated by either a Scheduled or HTTP Trigger. This may be null.
     - `Bot.Request.HttpMethod`: The HTTP method sent to the Trigger. This should currently always be `POST`.
	 - `Bot.Request.RawBody`: The raw body of the request, if it exists.
	 - `Bot.Request.ContentType`: The content type of the request.
	 - `Bot.Request.IsJson`: True if the content type is json. Otherwise false.
	 - `Bot.Request.IsForm`: True if this is a form submission. Otherwise false.
	 - `Bot.Request.Headers`: A JSON collection of the request headers.
	 - `Bot.Request.Form`: The form data. Only populated if `IsForm` is true.
	 - `Bot.Request.Query`: The querystring sent with the request, if one was present.
* `Bot.Secrets` <sup>C#</sup> `bot.secrets` <sup>Python, JavaScript</sup>
    - The interface to any skill secrets that might be set. See `Managing Secrets` for more.
* `Bot.ReplyAsync(response)` <sup>C#</sup> `bot.reply(response)` <sup>Python, JavaScript</sup>
    - The method skills must use to send responses back to the chat. This may be called multiple times.
* `Bot.ReplyLaterAsync(response, delay_in_seconds)`<sup>C#</sup> <br>`bot.reply_later(response, delay_in_seconds)`<sup>Python</sup><br>`bot.replyLater(response, delay_in_seconds)`<sup>JavaScript</sup>
  - Send a response back to the chat after `delay_in_seconds` has elapsed. This may be called multiple times, and is the preferred method for delaying responses.


## Managing Data <sup>C#</sup>
Abbot includes a simple persistence layer that makes it easy for your skills to store and retrieve data. You can access Abbot's brain with `Bot.Brain`. The methods that are included in `Bot.Brain` are:
* `WriteAsync(Key, Value)`: Save `Value` with a key of `Key`.
* `GetAsync(Key)`: Get the value stored with key `Key`.
* `GetKeysAsync(Key?)`: Get all keys that match `Key`. `Key` can be empty and will return all keys.
    * note: This is not currently implemented in Python or JavaScript.
* `GetAllAsync(Key?)`: Get all records where keys match `Key`. This supports fuzzy matching, so partial matches will be returned. `Key` can be empty and will return all keys and values.
    * note: This is not currently implemented in Python or JavaScript.
* `DeleteAsync(Key)`: Delete the value stored with key `Key`.

## Managing Secrets <sup>C#</sup>
Secrets are a special kind of data, and can be used to store things like authentication tokens or other configuration items that you prefer to exclude from your skill. Secrets can only be set from https://ab.bot, and are specific to a single skill. Since developers can read data from your secrets, be careful about the data that you store there -- passwords should never be stored in a Secret, for example. Secrets can be read using a similar interface to Abbot's brain:
* `GetAsync(Key)`: Get the Secret with the key of `Key`

## The Mentions Collection <sup>C#</sup>
`Bot.Mentions` contains a list of all mentions that were found in the user's text. The `ToString()` method on each mention will return an appropriately formatted username mention to the chat system (for example, `<@U92394113>` in Slack). Skill developers can also use any of the other fields available in the Mention object in their skills.

### The Mention Object <sup>C#</sup>
The Mention object has these fields:
* `Id`: The id of the person or bot that was mentioned. This id is unique to the chat platform that was being used, and is not an Abbot user id.
* `UserName`: The user name of the person or bot that was mentioned. This name is determined by the chat platform, and is not an Abbot user name.
* `Name`: The display name of the person or bot that was mentioned. This is set by the user in the chat platform and may change over time.
If you are writing skills that rely on the Mention object, the `Id` is the only reliable field to use in keys and for comparison.

## Managing Data <sup>Python, JavaScript</sup>
Abbot includes a simple persistence layer that makes it easy for your skills to store and retrieve data. You can access Abbot's brain with `bot.brain`. The methods that are included in `bot.brain` are:
* `write(key, value)`: Save `value` with a key of `key`.
* `get(key)`: Get the value stored with key `key`.
* `list()`: Get all records.
* `delete(key)`: Delete the value stored with key `key`.

## Managing Secrets <sup>Python, JavaScript</sup>
Secrets are a special kind of data, and can be used to store things like authentication tokens or other configuration items that you prefer to exclude from your skill. Secrets can only be set from https://ab.bot, and are specific to a single skill. Since developers can read data from your secrets, be careful about the data that you store there -- passwords should never be stored in a Secret, for example. Secrets can be read using a similar interface to Abbot's brain (but with `bot.secrets`):
* `get(key)`: Get the Secret with the key of `key`

## The Mentions Collection <sup>Python, JavaScript</sup>
`bot.mentions` contains a list of all mentions that were found in the user's text. The `toString()` or `str` method on each mention will return an appropriately formatted username mention to the chat system (for example, `<@U92394113>` in Slack). Skill developers can also use any of the other fields available in the Mention object in their skills.

### The Mention Object <sup>Python, JavaScript</sup>
The Mention object has these fields:
* `id`: The id of the person or bot that was mentioned. This id is unique to the chat platform that was being used, and is not an Abbot user id.
* `user_name` <sup>Python</sup>, `userName` <sup>JavaScript</sup>: The user name of the person or bot that was mentioned. This name is determined by the chat platform, and is not an Abbot user name.
* `name`: The display name of the person or bot that was mentioned. This is set by the user in the chat platform and may change over time.
If you are writing skills that rely on the Mention object, the `id` is the only reliable field to use in keys and for comparison.
