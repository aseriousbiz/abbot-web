import {Controller} from "@hotwired/stimulus";
import {getBindKey, getValuesHash, setValue} from "../data-bind";

/*
 * A Stimulus controller for a modal dialog. The controller can be directly applied to the modal element. Alternatively,
 * use the modal-target="modal" attribute to specify the modal element.
 *
 * <div id="login" data-controller="modal">
 *   <input type="text" data-modal-target="value">
 *   <button data-action="modal#dismiss">Dismiss</button>
 *   <button data-action="modal#submit">Submit</button>
 * </div>
 *
 * The modal is opened by raising a `modal:open` event on the modal. For example:
 *
 *     modal.dispatchEvent(new CustomEvent("modal:open")); // Opens the modal.
 *
 * To pre-populate the modal, pass in a Map<string, string> of key value pairs in the detail of the event. For example:
 *
 *      const values = getHashValues(bindTargets); // Creates a key/value map from the array of elements.
 *      modal.dispatchEvent(new CustomEvent("modal:open", detail: values));
 *
 * The keys must match the `data-bind-key` attribute of value targets within the modal.
 *
 * The `modal#submit` action raises a custom `modal:submit` event on the modal. The `detail` is a hash of modal values.
 * Modal values are populated from all the `value` targets. The key is the value of the `data-bind-key` attribute. The
 * value is the value of the element.
 *
 * document.addEventListener("modal:submit", (event) => {
 *  console.log(event.detail.get('key')); // The modal value for the key element.
 * });
 *
 * When the modal is dismissed, a `modal:dismiss` event is raised on the modal.
 */
export default class extends Controller<Element> {
    // Stimulus controller for a modal dialog.

    static targets = [
        "modal",   // The modal element to display.
        "dismiss", // The button element that closes the modal.
        "submit",  // The button element that submits the modal.
        "value"    // The element that contains the value of the modal when submitted.
    ];

    declare modalTarget: HTMLElement;
    declare hasModalTarget: boolean;
    declare modal: HTMLElement;
    declare valueTargets: HTMLElement[];

    initialize() {
        this.modal = this.hasModalTarget ? this.modalTarget : this.element as HTMLElement;
    }

    connect() {
        this.modal.addEventListener("modal:open", this.handleModalOpenEvent);
    }

    disconnect() {
        this.modal.removeEventListener("modal:open", this.handleModalOpenEvent);
    }

    private populate(values: Map<string, string>) {
        for (const valueTarget of this.valueTargets) {
            const key = getBindKey(valueTarget);
            if (values.has(key)) {
                setValue(valueTarget, values.get(key));
            }
        }
    }

    open() {
        this.modal.classList.add("is-active");
    }

    dismiss() {
        this.hide();
        this.modal.dispatchEvent(new CustomEvent("modal:dismiss"));
    }

    submit() {
        const values = getValuesHash(this.valueTargets);
        this.modal.dispatchEvent(new CustomEvent("modal:submit", { detail: values }));
        this.hide();
    }


    private hide() {
        this.modal.classList.remove("is-active");
    }

    private handleModalOpenEvent = (event: CustomEvent) => {
        const detail = event.detail;
        if (detail) {
            this.populate(detail);
        }
        this.open();
    }
}
