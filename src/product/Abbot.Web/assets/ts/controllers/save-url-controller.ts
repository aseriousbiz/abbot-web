import { Controller } from "@hotwired/stimulus";
import { organizationId } from "../env";
import logger from "../log";

const log = logger("save-url");

export default class extends Controller<HTMLElement> {
    static values = {
        key: String
    }
    declare keyValue: string;

    connect() {
        // Save our current URL to local storage
        let key = `save-url:${this.keyValue}`;
        if (organizationId) {
            key += `:${organizationId}`;
        }
        const value = window.location.href;
        log.verbose(`Saving URL ${value} to ${key}`, { key, value });
        window.localStorage.setItem(key, value);
    }
}
