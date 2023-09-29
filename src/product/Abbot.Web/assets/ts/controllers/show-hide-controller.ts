import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = ["subject", "toggler"];
    declare subjectTarget: HTMLElement;
    declare togglerTarget: HTMLElement;
    declare hasTogglerTarget: boolean;

    connect() {
        this.update();
    }

    update() {
        if (this.hasTogglerTarget && this.togglerTarget instanceof HTMLInputElement) {
            this.togglerTarget.checked ? this.show() : this.hide();
        }
    }

    toggle() {
        if (this.subjectTarget.classList.contains('hidden')) {
            this.show();
        } else {
            this.hide();
        }
    }

    show() {
        this.subjectTarget.classList.remove('hidden');
    }

    hide() {
        this.subjectTarget.classList.add('hidden');
    }
}
