import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = [
        // The editable content.
        "content",

        // The hidden input that will be updated with the content.
        "hidden"];

    static values = {
        // The URL to post the content to.
        "url": String
    };

    declare contentTarget: HTMLElement;
    declare hiddenTarget: HTMLInputElement;
    declare contentChanged: boolean;

    contentTargetConnected(element: HTMLElement) {
        element.dataset.action ||= "blur->editable#submit input->editable#update";
        element.addEventListener('keypress', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                element.blur();
            }
        });
    }

    update() {
        this.hiddenTarget.value = this.contentTarget.innerText;
        this.contentChanged = true;
    }

    submit() {
        if (this.contentChanged) {
            this.hiddenTarget.form.requestSubmit();
            this.contentChanged = false;
        }
    }
}
