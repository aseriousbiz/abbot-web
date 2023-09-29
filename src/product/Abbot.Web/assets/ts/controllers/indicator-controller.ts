import { Controller } from "@hotwired/stimulus";
import logger from "../log";

const log = logger("indicator");

export default class extends Controller<HTMLElement> {
    connect() {
        // Just to make sure
        this.element.classList.add("invisible");

        this.element.ownerDocument.addEventListener("turbo:submit-start", (e) => {
            // Ok, we've submitted a form. Is it the form we're in?
            if (e.target instanceof HTMLElement && e.target.contains(this.element)) {
                // It is!
                log.verbose("Parent form submitted. Showing indicator.");
                this.element.classList.remove("invisible");
            }
        });

        this.element.ownerDocument.addEventListener("turbo:before-stream-render", () => {
            // Hide ourselves. Doesn't matter if this is from the same form or not.
            // If it was our form, we want to become hidden.
            // If it wasn't our form, we're already hidden and re "adding" the class is harmless
            this.element.classList.add("invisible");
        });
    }
}
