import { Controller } from "@hotwired/stimulus";
import {setValue, getBindKey, getValuesHash} from "../data-bind";

/*
 * A Stimulus controller that listens for a `modal:submit` event and updates bound elements
 * with value elements from a modal controller.
 *
 * <div id="login" data-controller="modal">
 *   <input data-bind-key="name" type="text" data-modal-target="value">
 *   <input data-bind-key="pwd" type="text" data-modal-target="value">
 *   <button data-modal-target="dismiss">Dismiss</button>
 *   <button data-modal-target="submit">Submit</button>
 * </div>
 *
 * And the following modal listener.
 *
 * <form data-controller="modal-submit-listener" data-modal-listener-modal-value="login">
 *   <input
 *        data-bind-key="name"
 *        type="hidden"
 *        data-modal-listener-target="bind"
 *        value="" />
 *   <input
 *        data-bind-key="pwd"
 *        type="hidden"
 *        data-modal-listener-key="pwd"
 *        data-modal-listener-target="bind"
 *        value="" />
 * </form>
 *
 * The value of the hidden inputs will be updated to the values of the corresponding modal value elements when the
 * modal is submitted matched up by the `data-bind-key` attribute.
 *
 * This controller can also be used to open a modal with pre-populated values bound to do the `bind` targets by
 * invoking the `modal-listener#open` action.
 */
export default class extends Controller<Element> {
    static targets = [
        "bind",    // Applied to elements that are bound to modal values.
        "open" // The (optional) element that opens the modal with pre-populated bound values.
    ];

    static values = {
        modal: String, // The Id of the modal.
        bind: String   // The id of the element in the modal to bind this element value to.
    }

    declare modal: HTMLElement;
    declare modalValue: string;
    declare bindTargets: HTMLElement[];
    declare hasBindTarget: boolean;

    initialize() {
        this.modal = document.getElementById(this.modalValue);
    }

    connect() {
        if (this.modal) {
            this.modal.addEventListener("modal:submit", this.onModalSubmit);
        }
    }

    disconnect() {
        if (this.modal) {
            this.modal.removeEventListener("modal:submit", this.onModalSubmit);
        }
    }

    /* Opens a modal pre-populated with the values of the bind targets.. */
    open(event: Event) {
        const values = this.hasBindTarget
            ? getValuesHash(this.bindTargets)
            : {};

        this.modal.dispatchEvent(new CustomEvent("modal:open", { detail: values }));
        event.preventDefault();
    }

    private onModalSubmit = (event: CustomEvent) => {
        if (this.hasBindTarget) {
            const values = event.detail;
            this.bindTargets.forEach((target) => {
                const key = getBindKey(target);
                if (values.has(key)) {
                    setValue(target, values.get(key));
                }
            });
        }
    }
}
