import {Controller} from "@hotwired/stimulus";

/*
 * A simple Stimulus controller that opens a modal when the element the controller is applied to is clicked.
 *
 * <button data-controller="modal-launcher" data-modal-launcher-modal-value="login">Login</button>
 *
 * If you need to launch a modal with pre-populated values retrieved from a set of elements,
 * use the `modal-listener` controller.
 */
export default class extends Controller<Element> {
    static values = {
        modal: String // The Id of the modal to open.
    };

    declare modalValue: string;

    initialize() {
        if (this.element instanceof HTMLElement) {
            // Set the default action to open the modal.
            this.element.dataset.action ||= "modal-launcher#open";
        }
    }

    open(event: Event) {
        const modal = document.getElementById(this.modalValue);
        if (!modal) {
            throw new Error(`Could not find modal with id ${this.modalValue}`);
        }
        modal.dispatchEvent(new CustomEvent("modal:open"));
        event.preventDefault();
    }
}
