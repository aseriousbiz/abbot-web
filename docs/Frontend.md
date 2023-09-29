# Frontend Development and JavaScript

We primarily use [Turbo](http://turbo.hotwired.dev) and [Stimulus](https://stimulus.hotwired.dev) to handle our JavaScript interations.
In general, **avoid `<script>` tags and other embedded JavaScript**.

## Logging

We have a simple logging "framework" in place in our JS code.
Logs are _automatically_ dumped to the browser console in the "Development" environment.
You can enable logs in production by going to your browser console and running `setLogging(true);`.
This will store a value in the local storage key `abbot:logging` that enables logging until you run `setLogging(false);`.

To write logs, get a logger using the `logger` function:

```typescript
import logger from "../log";
const log = logger("category");
```

Then just use `log` like you would `console`:

```typescript
log.log("Something", "something", "something");
log.error("Something", "something", "something");
log.warn("Something", "something", "something");
```

All log messages will have the 'category' value you provided to `logger`.
You can also create "child" loggers using `log.child('subcategory')`.
This just creates a new logger with the category `category:subcategory`.
You can create infinitely-deep child logger categories.

## Turbo

[Turbo](https://turbo.hotwired.dev) is an "Accelerator" framework.
It hijacks all link clicks and form posts, makes the corresponding request using `fetch`, retrieves the HTML from the server and then swaps it in to the DOM.
This means clicks never actually trigger standard browser navigation.
This model has a few consequences you need to understand when developing front-end UI:

1. Server-side behavior is _fine_! It will still appear client-side to the user.
2. Use Stimulus Controllers to add JavaScript behavior (see below)
3. **DO NOT** run JavaScript in response to `DOMContentLoaded`. This will only be triggered on the initial page load and not when navigating around using Turbo. If you _must_ hook an "onload" event, hook **both** `DOMContentLoaded` (for the initial load) and `turbo:render` (for subsequent Turbo renders).

Over time, we can also adopt features like [Turbo Frames](https://turbo.hotwired.dev/handbook/frames) and [Turbo Streams](https://turbo.hotwired.dev/handbook/streams) to minimize the rendering time.

## Stimulus

Stimulus is a way to attach JavaScript behavior to DOM elements.
Define controllers in `src/product/Abbot.Web/assets/ts/controllers`, then attach them to elements using the `data-controller` attribute.
For a concrete example, see the `clipboard-controller.ts` file, and look at the `CopyBoxTagHelper` class.
Even though this uses a Tag Helper, the Tag Helper is _just_ rendering markup.

### AutoControllers

We have a custom extension to Stimulus called "AutoControllers", defined in `autocontrollers.ts`.
This allows you to annotate any Stimulus controller with the `autobind` decorator.
Provide a set of selectors to the decorator, and the extension will automatically attach the controller on which you placed the attribute to any element that matches the selector, whenever one appears in the DOM.
For example, the following controller allows you to make any button pop up a confirm prompt by simply adding a `data-confirm` attribute to it, with the desired message:

```typescript
import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";

@autobind("[data-confirm]")
export default class extends Controller<HTMLElement> {
    confirmMessage: string;
    connect() {
        this.confirmMessage = this.element.dataset.confirm || "Are you sure?";
        this.element.addEventListener("click", (evt) => this.confirm(evt));
    }

    confirm(evt: Event) {
        if(!window.confirm(this.confirmMessage)) {
            evt.preventDefault();
        }
    }
}
```

Any time an element with `data-confirm` appears, the autocontroller system will attach `data-controller="confirm"` to that element.