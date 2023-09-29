import * as Turbo from "@hotwired/turbo";
import { Controller } from "@hotwired/stimulus";
import logger from "../log";
import { organizationId } from "../env";

const log = logger("read-url");

export default class extends Controller<HTMLElement> {
    static values = {
        key: String
    }
    declare keyValue: string;

    navigateToSavedUrl(evt: Event) {
        // Check for a current URL in local storage
        let key = `save-url:${this.keyValue}`;
        if (organizationId) {
            key += `:${organizationId}`;
        }
        const value = window.localStorage.getItem(key);
        if (value) {
            evt.preventDefault();
            log.verbose(`Read URL ${value} from ${key}`, { key, value });
            Turbo.visit(value);
        }
    }
}
