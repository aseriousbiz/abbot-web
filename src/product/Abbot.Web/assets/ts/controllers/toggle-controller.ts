import { Controller } from "@hotwired/stimulus";

/*
  Simple toggler. Binds checkbox clicks to methods on this controller.
 */
export default class extends Controller<HTMLElement> {
    static targets = [
        "source",   // The checkbox that does the toggling.
        "dependent" // The item dependent on the toggle.
    ];

    // The checkbox used to toggle all or none.
    declare sourceTarget: HTMLInputElement;

    // The checkbox items
    declare dependentTarget: HTMLInputElement;

    connect() {
        this.update();
    }

    sourceTargetConnected(element: HTMLInputElement) {
        element.dataset.action ||= "toggle#update";
    }

    enable() {
        this.sourceTarget.checked = true;
        this.update();
    }

    update() {
        this.dependentTarget.disabled = !this.sourceTarget.checked;
        if (this.sourceTarget.checked) {
            this.dependentTarget.focus();
        }
    }
}