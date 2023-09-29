import { Controller } from "@hotwired/stimulus";
import logger from "../log";

const log = logger('form-prefill');

export default class extends Controller<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement> {
    static values = {
        key: String
    }
    declare hasKeyValue: boolean
    declare keyValue: string

    key: string;

    connect() {
        this.key = this.hasKeyValue
            ? this.keyValue
            : this.element.name || this.element.id;

        // Now, check the fragment for that key
        log.verbose("Parsing fragment", { hash: window.location.hash });
        const parsedFragment = new URLSearchParams(window.location.hash.substring(1));
        if (parsedFragment.has(this.key)) {
            this.element.value = parsedFragment.get(this.key) || "";
        }
    }
}