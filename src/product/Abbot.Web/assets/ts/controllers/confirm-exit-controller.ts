import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = [
        // The form to check for changes.
        "form",

        // The element that controls whether we should confirm or not.
        "editor"];

    static values = {
        message: String
    }

    declare formTarget: HTMLFormElement;
    declare messageValue: string;

    initialize() {
        this.messageValue ||= "You have unsaved changes. Press OK to continue without saving.";
    }

    editorTargetConnected() {
        document.addEventListener('turbo:before-visit', this.confirm);
        window.onbeforeunload = evt => {
            if (this.formIsDirty(this.formTarget)) {
                // For IE and Firefox
                if (evt) {
                    evt.returnValue = this.messageValue;
                }
                // For Safari
                return this.messageValue;
            }
        };
    }

    editorTargetDisconnected() {
        document.removeEventListener('turbo:before-visit', this.confirm);
        window.onbeforeunload = null;
    }

    confirm = (evt: Event) => {
        if (this.formIsDirty(this.formTarget)) {
            if (!window.confirm(this.messageValue)) {
                evt.preventDefault();
            }
        }
    }

    /**
     * Determines if a form is dirty by comparing the current value of each element
     * with its default value.
     *
     * @param {HTMLFormElement} form - The form to be checked.
     * @return {Boolean} <code>true</code> if the form is dirty,
     * <code>false</code> otherwise.
     *
     * CREDIT: Adapted from https://stackoverflow.com/a/155812
     */
    formIsDirty(form) {
        for (const element of form.elements) {
            switch (element.type) {
                case "checkbox":
                case "radio":
                    if (element.checked !== element.defaultChecked) {
                        console.log('Form dirty because element was checked or unchecked.')
                        return true;
                    }
                    break;
                case "hidden":
                case "password":
                case "text":
                case "textarea":
                case "date":
                case "datetime-local":
                case "email":
                case "month":
                case "number":
                case "search":
                case "tel":
                case "time":
                case "url":
                case "week":
                case "color": {
                        const value = element.value.replace(/\r/g, '');
                        const defaultValue = element.defaultValue.replace(/\r/g, '');
                        if (value !== defaultValue) {
                            console.log('Form dirty text input value was changed from the default value.');
                            return true;
                        }
                    }
                    break;
                case "select-one":
                case "select-multiple":
                    for (const option of element.options) {
                        if (option.selected !== option.defaultSelected) {
                            console.log('Form dirty because selected item changed.');
                            return true;
                        }
                    }
                    break;
            }
        }
        return false;
    }
}
