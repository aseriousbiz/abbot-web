import { Controller } from "@hotwired/stimulus";
import logger from "../log";
import { StimulusEvent } from "../stimulus";
import { createIconSpan } from "../icons";
import { TurboSubmitEndEvent } from "@hotwired/turbo";

const log = logger("autocheckbox");

/*
 * This controller is used to automatically submit a form when a checkbox is toggled.
 */
export default class extends Controller<HTMLFormElement> {
    activeCheckbox?: HTMLInputElement;
    activeIndicator?: HTMLSpanElement;

    connect() {
        this.element.addEventListener("turbo:submit-end", this.submitHandler);
    }

    disconnect() {
        this.element.removeEventListener("turbo:submit-end", this.submitHandler);
    }

    async toggle(evt: StimulusEvent) {
        if (evt.target instanceof HTMLInputElement && evt.target.type === "checkbox") {
            log.verbose(`Setting checkbox to '${evt.target.checked}'`, { checked: evt.target.checked, target: evt.target });
            this.activeCheckbox = evt.target;

            // The checkbox should be in the desired state.
            // However, we need to submit the form before we can change the state.
            // So, we capture the current state and then submit the form.
            if (!evt.target.form) {
                throw new Error("Checkbox does not have a form");
            }

            this.setIndicator(createIconSpan("fa fa-spinner fa-spin"))

            evt.target.form.requestSubmit();
        } else {
            log.warn("Element is not a checkbox", { element: evt.target });
        }
    }

    submitHandler = (evt: TurboSubmitEndEvent) => {
        if (evt.target !== this.element) {
            log.warn("Received turbo:submit-end for the wrong form", { target: evt.target });
        } else {
            if (evt.detail.success) {
                if (this.activeCheckbox) {
                    this.activeCheckbox.indeterminate = false;
                }

                const successIndicator = createIconSpan("fa fa-check text-green-500");
                successIndicator.dataset.controller = 'alert';
                successIndicator.dataset.alertDismissAfterValue = "2500";
                this.setIndicator(successIndicator);
            }
            else {
                if (this.activeCheckbox) {
                    this.activeCheckbox.checked = !this.activeCheckbox.checked;
                    this.activeCheckbox.indeterminate = false;
                }

                const err = createIconSpan("fa fa-triangle-exclamation text-yellow-500");
                err.setAttribute("data-tooltip", "An error occurred while saving the setting.");
                this.setIndicator(err);
            }
        }
    }

    setIndicator(indicator: HTMLSpanElement) {
        this.clearIndicator();

        this.activeIndicator = indicator;
        this.element.appendChild(indicator);
    }

    clearIndicator() {
        if (this.activeIndicator) {
            this.activeIndicator.remove();
            this.activeIndicator = undefined;
        }
    }
}
