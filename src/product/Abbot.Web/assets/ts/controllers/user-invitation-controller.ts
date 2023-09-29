import { Controller } from "@hotwired/stimulus";

/*
 * This controller is used to handle the user invitation form.
 */
export default class extends Controller<HTMLElement> {
    static targets = ["subject", "item"];
    declare subjectTarget: HTMLElement;
    declare itemTargets: HTMLElement[];

    connect() {
        this.update();
    }

    update() {
        if (this.itemTargets.length === 0) {
            this.hide();
        }
        else {
            this.show();
        }
    }

    itemTargetConnected() {
        this.update();
    }

    itemTargetDisconnected() {
        this.update();
    }

    subjectTargetConnected() {
        this.update();
    }

    subjectTargetDisconnected() {
        this.update();
    }

    show() {
        this.subjectTarget.style.display = 'inherit';
    }

    hide() {
        this.subjectTarget.style.display = 'none';
    }
}
