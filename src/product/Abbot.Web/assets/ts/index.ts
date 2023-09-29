import * as Turbo from '@hotwired/turbo';
import * as Stimulus from '@hotwired/stimulus';
import { definitionsFromContext } from '@hotwired/stimulus-webpack-helpers';
import * as timeago from 'timeago.js';
import { ValidationService } from 'aspnet-client-validation';

import './analytics';
import './telemetry';
import { legacyOnLoad } from './legacy';
import { isDevelopment } from './env';
import { useAutocontrollers } from './autocontrollers';
import logger from './log';
import registerProviders from './validators';
import { updateDefaultValues } from "./legacy/forms";

const validationService = new ValidationService(logger("aspnet-client-validation"));
registerProviders(validationService);

validationService.bootstrap({
    root: document.documentElement,
});

// Get FontAwesome rolling
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const { FontAwesome } = (window as any);
FontAwesome?.dom.watch();

addEventListener("turbo:before-stream-render", ((event: CustomEvent) => {
    const fallbackToDefaultActions = event.detail.render;

    // Custom stream actions: https://turbo.hotwired.dev/handbook/streams#custom-actions
    event.detail.render = function (streamElement) {
        switch (streamElement.action) {
            case "location": {
                const url = streamElement.querySelector('template').innerHTML;
                switch (streamElement.target) {
                    case "push":
                        window.history.pushState(null, "", url);
                        break;
                    case "replace":
                        window.history.replaceState(null, "", url);
                        break;
                    default:
                        throw new Error(`Unknown location target: ${streamElement.target}`);
                }
            }
                break;
            case "defaults": {
                const form = document.getElementById(streamElement.target) as HTMLFormElement;
                if (form) {
                    updateDefaultValues(form);
                }
                break;
            }
            default:
                fallbackToDefaultActions(streamElement);
        }
    }
}))

const timeagoScan = (node: ParentNode) => {
    node.querySelectorAll<HTMLTimeElement>('time.timeago[datetime]')
        .forEach(e => timeago.render(e));
}

// Run the legacy on load code on the first load, and on every subsequent turbo:render.
document.addEventListener("DOMContentLoaded", () => {
    timeagoScan(document.body);
    legacyOnLoad();
});
document.addEventListener("turbo:render", () => {
    // timeagoScan() is in beforeRender to prevent flicker
    legacyOnLoad();
});
const beforeRender = async (event: Event, node: ParentNode, resumeCallback?: (_: unknown) => void) => {
    if (resumeCallback) {
        event.preventDefault();
    }

    const templates = Array.from(node.querySelectorAll<HTMLTemplateElement>('template'));
    const nodes = [node, ...templates.map(t => t.content)];
    await Promise.all(
        nodes.map(async node => {
            await FontAwesome?.dom.i2svg({ node });
            timeagoScan(node);

            // TODO: Type check is necessary until https://github.com/haacked/aspnet-client-validation/pull/45
            if (node instanceof HTMLElement) {
                validationService.scan(node);
            }
        }));

    if (resumeCallback) {
        resumeCallback(undefined); // types are wrong; parameter is ignored
    }
};
document.addEventListener("turbo:before-render", (event: Turbo.TurboBeforeRenderEvent) => {
    beforeRender(event, event.detail.newBody, event.detail.resume);
});
document.addEventListener("turbo:before-frame-render", (event: Turbo.TurboBeforeFrameRenderEvent) => {
    beforeRender(event, event.detail.newFrame, event.detail.resume);
});
document.addEventListener("turbo:before-stream-render", (event: Turbo.TurboBeforeStreamRenderEvent) => {
    beforeRender(event, event.detail.newStream);

    const defaultRender = event.detail.render
    event.detail.render = async (streamElement) => {
        await defaultRender(streamElement)

        // Need to apply timeago to the new/modified elements
        streamElement.targetElements
            .filter(e => e instanceof Element)
            .forEach((e: Element) => timeagoScan(e));
    }
});

if (isDevelopment) {
    // Hook all turbo events and log them
    [
        "turbo:click",
        "turbo:before-visit",
        "turbo:visit",
        "turbo:submit-start",
        "turbo:before-fetch-request",
        "turbo:before-fetch-response",
        "turbo:submit-end",
        "turbo:before-cache",
        "turbo:before-render",
        "turbo:before-frame-render",
        "turbo:before-stream-render",
        "turbo:render",
        "turbo:load",
        "turbo:frame-render",
        "turbo:frame-load",
        "turbo:fetch-request-error",
    ].forEach((event) => {
        document.addEventListener(event, (e) => {
            logger("turbo").log(`Turbo event '${event} triggered`, { eventName: event, detail: e });
        });
    });
}

// Start turbo
Turbo.start();

// Start Stimulus
const app = Stimulus.Application.start();
window['Stimulus'] = app;

// Auto-load controllers from './controllers'
const context = require.context("./controllers", true, /\.[t|j]s$/);
app.load(definitionsFromContext(context));

if (isDevelopment) {
    const log = logger("Stimulus");
    app.logDebugActivity = (identifier, functionName, detail?) => {
        log.verbose(`${identifier} ${functionName}`, {
            identifier,
            functionName,
            detail
        });
    };
}

// Auto-bind controllers
useAutocontrollers(app);
