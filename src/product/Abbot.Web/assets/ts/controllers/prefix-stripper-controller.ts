import {Controller} from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = [
        // The input element that contains the text to be stripped
        "prefix",

        // The input element that contains the stripped text
        "input"
    ];

    declare prefixTarget: HTMLInputElement;
    declare inputTargets: HTMLInputElement[];

    connect() {
        this.update();
    }

    update() {
        const prefix = this.prefixTarget.value || '';
        this.inputTargets.forEach(input => {
            const originalValue = input.dataset.originalValue || input.value;
            if (originalValue.startsWith(prefix)) {
                // Enable the input and row.
                input.disabled = false;
                input.value = originalValue.substring(prefix.length).trim();
                const row = input.closest('[role="row"]');
                if (row) {
                    row.classList.remove('bg-gray-200');
                    const note = row.querySelector('[role="note"]');
                    if (note) {
                        note.classList.add('hidden');
                    }
                    // Find the associated hidden input.
                    const hiddenInput = row.querySelector('input[type="hidden"]') as HTMLInputElement;
                    if (hiddenInput) {
                        hiddenInput.disabled = false;
                    }
                }
            }
            else {
                // Disable the input and row.
                input.disabled = true;
                const row = input.closest('[role="row"]');
                if (row) {
                    row.classList.add('bg-gray-200');
                    const note = row.querySelector('[role="note"]');
                    if (note) {
                        note.classList.remove('hidden');
                    }
                    // Find the associated hidden input.
                    const hiddenInput = row.querySelector('input[type="hidden"]') as HTMLInputElement;
                    if (hiddenInput) {
                        hiddenInput.disabled = true;
                    }
                }
            }
        });
    }

    prefixTargetConnected(element: HTMLInputElement) {
        element.dataset.action ||= "input->prefix-stripper#update";
    }
}