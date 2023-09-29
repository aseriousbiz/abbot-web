import {Controller} from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static values = {
        dismissAfter: Number
    }

    declare dismissing: boolean;
    declare readonly dismissAfterValue: number;

    connect() {
        if (this.dismissAfterValue > 0) {
            setTimeout(() => this.dismiss(), this.dismissAfterValue);
        }
    }

    dismiss() {
        if (this.dismissing) {
            return;
        }
        this.dismissing = true;

        if (this.element) {
            this.element.classList.add('fade-out');
            setTimeout(() => this.remove(), 750);
        }
    }

    remove() {
        const element = this.element;
        if (element) {
            const parent = element.parentNode;
            // Remove an element from the page. This is used after the element has been dismissed or hidden.
            if (parent) {
                // The element may have already been removed by the time this is fired.
                parent.removeChild(element);
            }
            this.dismissing = false;
        }
    }
}
