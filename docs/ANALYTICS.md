# Analytics

How we approach analytics. See also, [Telemetry](Telemetry.md). The main difference between telemetry and analytics is telemetry is operational information produced by our running systems. These are primarily used to indicate if our systems are running well and help pinpoint the reason when they're not.

Analytics are more user and product focused data we collect to help us understand how our customers are using our product.

At the moment, we use [Segment](https://segment.com/docs/) to collect analytics and send them to various "Destinations" (such as Google Analytics).

## Guidelines

For the most part, use the JavaScript libraries for the web site to avoid double counting. This becomes especially important as we start to "Turbofy" our application. The server-side APIs make more sense for interactions with Slack. But of course, use your discretion.

We want to avoid personally identifiable information in our analytics calls, so we'll often use database IDs instead of names etc.

There are six calls in the basic tracking API, which answer specific questions:

* [Identify](https://segment.com/docs/connections/spec/identify/): Who is the user?
* [Track](https://segment.com/docs/connections/spec/track/): What are they doing?
* [Page](https://segment.com/docs/connections/spec/page/): What web page are they on?
* [Screen](https://segment.com/docs/connections/spec/screen/): What app screen are they on?
* [Group](https://segment.com/docs/connections/spec/group/): What account or organization are they part of?
* [Alias](https://segment.com/docs/connections/spec/alias/): What was their past identity?

### Identify

This identifies a user (and traits about the user) and ties all actions together to that user.

```js
analytics.identify("@Member.Id", {
    plan: "@Member.Organization.PlanType",
    organization: "@Member.OrganizationId"
});
```

According to their [Best Practices doc](https://segment.com/docs/connections/spec/best-practices-identify/)...

> A User ID should be a robust, static, unique identifier that you recognize a user by in your own systems. Because these IDs are consistent across a customer’s lifetime, you should include a User ID in Identify calls as often as you can.

Hence we'll use the database Id for the `Member`.

- [ ] Question, do we need an `organization` property here if we have `Group?`

### Track

This how you record any actions your users perform, along with any properties that describe the action. This is the one we'll probably call the most.

```js
analytics.track("Plan Purchased", {
    plan: "Business"
});
```

```csharp
Analytics.Client.Track(Member.Id, "Announcement Dialog Opened", new Properties() {
    { "message_id", Message.SlackMessageId }
});
```

[Segment docs recommend](https://segment.com/docs/getting-started/03-planning-full-install/#plan-your-track-events)

> We recommend starting with fewer events that are directly tied to one of your business objectives, to help avoid becoming overwhelmed by endless number of possible actions to track.

Also,

> Events should be generic and high-level, but properties should be specific and detailed.

And we'll try our best to follow their [recommended event naming conventions](https://segment.com/docs/getting-started/03-planning-full-install/#create-naming-conventions).

* __Pick a casing convention:__ We recommend Title Case for event names and snake_case for property names. Make sure you pick a casing standard and enforce it across your events and properties.

* __Pick an event name structure:__ As you may have noticed from our specs, we’re big fans of the Object (Blog Post) + Action (Read) framework for event names. Pick a convention and stick to it.

* __Don’t create event names dynamically:__ Avoid creating events that pull a dynamic value into the event name (for example, User Signed Up (11-01-2019)). If and when you send these to a warehouse for analysis, you end up with huge numbers of tables and schema bloat.

* __Don’t create events to track properties:__ Avoid adding values to event names when they could be a property. Instead, add values as a property. For example, rather than having an event called “Read Blog Post - Best Tracking Plans Ever”, create a “Blog Post Read” event and with a property like "blog_post_title":"Best Tracking Plans Ever".

* __Don’t create property keys dynamically:__ Avoid creating property names like "feature_1":"true","feature_2":"false" as these are ambiguous and very difficult to analyze

### Page

Used for pages of our website.

```js
analytics.page("Announcements", "Home");
```

### Screen

Primarily used for mobile screens, we can use this for our Slack dialogs.

```csharp
Analytics.Client.Screen(Member.Id, "Enable Conversation Tracking", new Properties() {
    { "enabled", "true" },
    { "room_id", Room.Id },
});
```

### Group

This how you associate the current user with a group or multiple groups.

Here's what the docs show as an example:

```js
analytics.group("0e8c78ea9d97a7b8185e8632", {
    name: "Initech",
    industry: "Technology",
    employees: 329,
    plan: "enterprise",
    "total billed": 830
});
```

Not sure if we want to put all that information in a JS call, or do we?

```js
analytics.group("@Organization.Id", {
    plan: "@PlanType",
});
```

