import * as Turbo from "@hotwired/turbo";
import { Controller } from "@hotwired/stimulus";
import logger from "../log";
import { StimulusEvent } from "../stimulus";
import { Action } from "@hotwired/turbo/dist/types/core/types";

const log = logger("turbo");

export default class extends Controller<Element> {
    visit(event: StimulusEvent<{ url?: string, action?: Action }>): void {
        log.verbose("Reloading frame");
        if (!event.params.url) {
            throw new Error("required parameter 'url' is missing");
        }

        const action = event.params.action || "replace";

        // Don't cache 
        Turbo.cache.exemptPageFromCache();
        Turbo.visit(event.params.url, { action: action });
    }
}
