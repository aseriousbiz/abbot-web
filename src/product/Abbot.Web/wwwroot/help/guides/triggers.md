# Responding to external events with Triggers
Abbot can respond to much more than just events raised from chat. Triggers give Abbot superpowers -- **[Http Triggers](#http-triggers)** allow Abbot to respond to events from the outside world; and **[Scheduled Triggers](#scheduled-triggers)** let Abbot execute Skills on a schedule you define.

Triggers are part of what make Abbot such a powerful tool for you and your team -- your automation is no longer limited to what happens inside of chat when you use Abbot Triggers.

Triggers are attached to channels inside of chat. Skills must already be created in order to attach a Trigger to a channel, but don't worry; you can always update your code after a Trigger has been attached.

## Http Triggers
Http Triggers allow Abbot to react to events from the outside world. When an Http Trigger is attached to an Abbot Skill, a URL unique to that Skill and channel is generated. The URL that is created by the `attach` Skill contains all the information that Abbot needs to know in order to deliver data to your Skill.

To attach an Http Trigger to your skill, first navigate to the specific channel in chat where you want the Trigger output to be sent. Attach the Skill to the channel by saying `@abbot attach <skillname>` where `skillname` is the name of the skill you'd like to attach. For example, if you had a skill called `gong` that you wanted to trigger every time your sales-tracking system recorded a sale, you'd type `@abbot attach gong`.

Once you've told Abbot that you want to attach a Trigger to the channel, it will respond with a link sending you to the Trigger management page. Clicking the link will take you to the Trigger management page.

### The Trigger Management page
The Trigger Management page is where you can see all the triggers attached to your Skill (both Http and Scheduled Triggers), as well as the channels those Triggers are attached to. There isn't much to configure for Http Triggers -- you can update the description to help your teammates understand what the Trigger will be used for. Beyond that, you can see who added the Trigger, when they added it, and most importantly, the secret URL for the Trigger. Clicking the URL will copy it to your clipboard. Abbot is configured to only accept `POST` events for Triggers, although that may change in the future. You can post any data you'd like to that endpoint, and your Skill will run.

What sort of things can you do with an Http Trigger? Well, anything! But we've found the best use cases involve reacting to events and data from the outside world. Http Triggers can recieve webhooks from other systems, and your skill code can access the data that's posted to the endpoint. Using Abbot's reply mechanism from your skill will send text into the channel where the trigger is attached. In the case of our `gong` Skill, we could parse the JSON that's posted to the Trigger to get some data about a deal that closed and post it into the channel, or notify a user that their deal is finally done. It's up to you! You can use the full power of an Abbot skill to do anything you'd like (including passing more information on to a third service).

## Scheduled Triggers
Scheduled Triggers let you set a schedule for Abbot to run Skills on your behalf. Skills run in this way can do everything that ordinary skills can do. In order to set up a Scheduled Trigger, use the `schdule` command. Let's repurpose the `gong` skill from above to tell us the time every ten minutes (changing the code for the skill will be an exercise left for you). In order to schedule the skill, first join the channel where the output should be sent. Once there, say `@abbot schedule gong`. Abbot will reply with a link to the Trigger management page like before (in fact, it's the same page as before -- all your triggers are managed here).

The details for the Scheduled Trigger do require more information than when using Http Triggers. Namely, what schedule the Trigger should use to run. The management page will say something like "The channel #your-amazing-channel receives replies when `gong` is triggered on the schedule: Never". If you click on the "Edit" button, a modal will pop up; allowing you to choose the schedule in which to run your skill. **Please note: all schedules use UTC.** Time zones are hard, and we are still working on a nice interface for dealing with them. For now, you'll need to do a little mental math if you want something to happen at a specific time locally.

In the _Cron Schedule_ area, you can choose the frequency in which your skills will run. We've prefilled the most common options we think you'll want, along with some configuration; but you can provide your own cron schedule if you would like. We don't allow any skills to execute more than once per 10 minutes right now, although that may change in the future.

The _Arguments_ section is where you can put any additional information to be sent to your skill. If people used the gong skill by saying `@abbot gong some words and things` you could add `some words and things` to the Arguments section and get the same result. This is optional, but useful if your skill expects some configuration flags.

Finally, you can add a description to your Trigger in order to keep track of what the Trigger is meant to accomplish.

Since Scheduled Triggers are attached to regular Abbot Skills, you can use them to schedule any kind of skill you can imagine, from checking the latest lottery numbers to generating a daily update report to share with your team. Your imagination is the only limit!

We use Triggers heavily as part of developing and running Abbot; more and more of our own infrastructure is built on top of Abbot because of how easy it is for us to develop, deploy, and maintain our functionality. We hope that you'll enjoy using Triggers as well. Please let us know if you have any questions, complaints, or feature suggestions for Triggers by using the `@abbot feedback` skill in chat or in the Bot Console.

Good luck and happy shipping!
