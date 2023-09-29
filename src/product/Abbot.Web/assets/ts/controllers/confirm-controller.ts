import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";

@autobind("[data-confirm]")
export default class extends Controller<HTMLElement> {
    confirmMessage?: string;

    connect() {
        this.confirmMessage = this.element.dataset.confirm;
        this.element.addEventListener("click", this.confirmHandler);
    }

    disconnect() {
        this.element.removeEventListener("click", this.confirmHandler);
    }

    confirm(evt: Event) {
        if(this.confirmMessage && !window.confirm(this.confirmMessage)) {
            evt.preventDefault();
        }
    }

    confirmHandler = (evt: Event) => this.confirm(evt);
}
