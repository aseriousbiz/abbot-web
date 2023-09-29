import {Controller} from "@hotwired/stimulus";

/*
 * You have to click it, to submit it. This sets up targets that can be clicked to submit a form.
 * Apply this controller to a form element.
 */
export default class extends Controller<HTMLElement> {
    static targets = ["submitter", "form"];

    declare formTarget: HTMLFormElement;
    declare hasFormTarget: boolean;

    declare form: HTMLFormElement;

    // The elements that can be clicked, to submit.
    declare submitterTargets: HTMLElement[];

    connect() {
        this.form = this.hasFormTarget
            ? this.formTarget
            : this.element as HTMLFormElement;
    }

    submitterTargetConnected(element: HTMLElement) {
        element.dataset.action ||= "click->click-submit#submit";
    }

    submit() {
        if (this.form) {
            this.form.requestSubmit();
        }
    }
}
