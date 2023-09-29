import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";
import { StimulusEvent } from "../stimulus";

@autobind("form:not([data-autobind='false']")
export default class extends Controller<HTMLElement> {
    static targets = ["resettable"];
    declare resettableTargets: HTMLInputElement[];

    submit(evt: StimulusEvent<{ action?: string }>) {
        if (evt.params.action) {
            // Rewrite the form action
            this.element.setAttribute("action", evt.params.action);
        }
        if (this.element instanceof HTMLFormElement) {
            this.element.requestSubmit();
        }
    }

    reset(evt: StimulusEvent) {
        evt.stopPropagation();
        evt.preventDefault();
        for (const target of this.resettableTargets) {
            target.value = "";
        }
    }
}
