/*
* Submit form when input changes with debounce
* Would be nice to merge this with click-submit-controller.ts someday.
*/
import {Controller} from "@hotwired/stimulus";
import { useDebounce } from "stimulus-use";

export default class extends Controller {
    static debounces = ["submit"];
    static targets = ["submitter", "form"];

    declare formTarget: HTMLFormElement;
    declare hasFormTarget: boolean;

    declare form: HTMLFormElement;

    // The elements that can be changed, to submit.
    declare submitterTargets: HTMLElement[];

    connect() {
        useDebounce(this, {});
        this.form = this.hasFormTarget
            ? this.formTarget
            : this.element as HTMLFormElement;
    }
    submitterTargetConnected(element: HTMLElement) {
        element.dataset.action ||= "input->input-submit#submit";
    }

    submit() {
        if (this.form) {
            this.form.requestSubmit();
        }
    }
}