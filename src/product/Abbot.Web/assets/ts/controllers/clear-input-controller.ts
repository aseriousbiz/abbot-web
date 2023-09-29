import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = ["input", "button"];

    declare inputTarget: HTMLInputElement;
    declare buttonTarget: HTMLButtonElement;
    declare hasButtonTarget: boolean;

    initialize() {
        if (this.hasButtonTarget) {
            this.buttonTarget.dataset.action = "click->clear-input#clear";
        }
    }

    buttonTargetConnected() {
        this.update();
    }

    inputTargetConnected(element: HTMLInputElement) {
        element.dataset.action ||= "input->clear-input#update";
    }

    update() {
        this.buttonTarget.hidden = this.inputTarget.value.length === 0;
    }

    clear() {
        this.inputTarget.value = "";
        this.inputTarget.form.requestSubmit();
    }
}
