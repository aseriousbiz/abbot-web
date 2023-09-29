import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";
import { useClickOutside } from "stimulus-use";

@autobind("details.dropdown")
export default class extends Controller<HTMLDetailsElement> {
    connect() {
        useClickOutside(this);

        // All submit buttons should dismiss the details dropdown.
        this.element.querySelectorAll('[type=submit]')
            .forEach((b: HTMLElement) => b.dataset.action ||= 'details-dropdown#clickOutside');
    }

    clickOutside() {
        this.element.open = false;
    }
}
