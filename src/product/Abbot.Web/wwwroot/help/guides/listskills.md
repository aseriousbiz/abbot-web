# List Skills
List skills are a special kind of skill that are built into Abbot. They are useful if you want to build a skill that returns a random item out of a list every time it is called.

Using Abbot, we could easily create a skill to accomplish that task like this:

```javascript
const _ = require('lodash');
let responses = ["Yes", "No", "Maybe", "I'm not sure, ask again later"]
bot.reply(_.sample(responses))
```

This works well as long as we don't need to change the list of potential answers. As soon as someone wants to add a new item to the list, it becomes much more complicated. If they have access to Abbot and are comfortable editing code, they could log in to https://ab.bot and change the skill. What if they wanted to add options from chat? The skill would have to be updated to add argument parsing and persistence:

```javascript
const _ = require('lodash');

if (bot.arguments.startsWith('add') {
	// Add a new response
} else if (bot.arguments.startsWith('remove') {
	// Remove a response
} else {
	// Return a random response
	let responses = await bot.brain.list();
	bot.reply(_.sample(responses))
}
```

This can get complicated quickly! Instead, Abbot makes it very easy to create skills like this on your own, from inside of chat. To create a new list skill in chat, say `@abbot list add <the name of your list skill>`. In this case, try `@abbot list add 8ball` (don't worry, list skills don't count as custom skills -- make as many of these as you'd like!).

Once Abbot creates the list skill, you can add options with `@abbot 8ball add ...`, like this:

* `@abbot 8ball add Yes`
* `@abbot 8ball add No`
* `@abbot 8ball add Maybe`
* `@abbot 8ball add Unclear, ask again later`


To use the skill, simply say `@abbot 8ball` in any channel where Abbot has been invited, and Abbot will respond with a random selection from the list.

You can remove items from Abbot's brain with `@abbot 8ball remove` and providing the full text of the item to remove.

You can always get more help from Abbot in chat by asking for it with `@abbot help list`.

Note that you can store both text and image links in Abbot lists. Image links will be treated as they are any time they're pasted into chat (which usually means the image will be displayed). So, get your favorite collections of gifs together and have fun making some list skills for you and your team!
