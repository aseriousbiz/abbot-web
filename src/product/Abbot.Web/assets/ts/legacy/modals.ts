/*
 * Sets up all modals and modal launchers in the site. A modal launcher has
 * a `data-modal-id` attribute with the Id of the modal it should launch.
 * Use the `data-modal-target` to specify the element that should be updated
 * with the modal value if any. By default, the modal value is an element
 * with the css class `modal-value`. When the success button (button.modal-success-button)
 * is clicked, the  modal target is updated with the `value` or `innerText` of
 * the modal value element. To override this behavior register a callback with
 * the object returned by this method.
 *
 * let modals = setupModals();
 * modals.callbacks[modal.id] = function(modalElement) {
 *   // ...
 *   return modalValue;
 * }
 *
 * EXAMPLE USAGE:
 *    <button data-modal-id="myModal">Launch Modal</button>
 *
 *    <dialog class="modal bg-gray-900 bg-opacity-75" id="myModal">
 *      <div class="rounded-lg p-2 border m-4 bg-white shadow-md"
 *        role="dialog" aria-labelledby="dialogTitle" aria-describedby="dialogDescription">
 *        <header class="bg-blue-500 text-white px-2 flex items-center">
 *            <h1 id="dialogTitle">Example header</h1>
 *            <button class="bg-white text-blue-500 hover:text-gray-900 flex p-1 my-1 ml-auto w-6 justify-center" aria-label="close">
 *                <i class="fa-solid fa-xmark" aria-label></i>
 *            </button>
 *        </header>
 *
 *        <section class="p-2">
 *            <p id="dialogDescription">This is an example modal.</p>
 *        </section>
 *      </div>
 *    </dialog>
 *
 * Note that the modal launcher can have a condition (query selector) used to determine if clicking the button
 * will show the modal.
 *
 *   <button data-modal-id="myModal" data-modal-condition="#doStuff:checked">Launch Modal</button>
 */

import { HTMLValueBearingElement } from "../types";

export default function setupModals() {
    const modals = {
        active: null, // The active modal, if any.

        // Methods called when the modal is initialized.
        initializers: {
            default: function(modalElement: HTMLElement, source) {
                // Update the value of elements in the modal form data bound to elements outside the modal.
                const modalValueInput = modalElement.querySelector<HTMLValueBearingElement>('.modal-value');
                if (modalValueInput) {
                    modalValueInput.value = source.value;
                }
                else {
                    console.log('Did not find any element with class "modal-value"');
                }

                const targetElements = modalElement.querySelectorAll<HTMLValueBearingElement>('[data-modal-bind-to]');
                targetElements.forEach(target => {
                    const source = modalElement.ownerDocument.querySelector<HTMLValueBearingElement>(`#${target.dataset.modalBindTo}`);
                    if (!source) {
                        return;
                    }
                    target.value = source.value;
                });
            }
        },

        // Methods called when the success button of the modal is clicked.
        callbacks: {
            default: function(modalElement: HTMLElement) {
                // Update the value of elements data bound to form elements in the modal.
                const targetElements = modalElement.querySelectorAll<HTMLValueBearingElement>('[data-modal-bind-to]');
                targetElements.forEach(source => {
                    const target = modalElement.ownerDocument.querySelector<HTMLValueBearingElement>(`#${source.dataset.modalBindTo}`);
                    if (!target) {
                        return;
                    }
                    target.value = source.value;
                });

                const modalValueInput = modalElement.querySelector<HTMLValueBearingElement>('.modal-value');
                if (modalValueInput) {
                    const value = modalValueInput.value || modalValueInput.innerText;
                    return {success: true, value: value }
                }

                return { success: true, value: null }
            }
        } // Set of callback methods,
    };

    document.querySelectorAll<HTMLElement>('[data-modal-id]')
        .forEach(launcher => {
            const modal = document.getElementById(launcher.dataset.modalId);
            if (!modal) {
                console.error(`Could not find a modal with the id ${launcher.dataset.modalId}`);
                return;
            }

            const update = document.querySelector<HTMLValueBearingElement>(`#${launcher.dataset.modalTarget}`);

            // Register all the close buttons.
            modal.querySelectorAll('[aria-label="close"]')
                .forEach(btn => {
                    btn.addEventListener('click', e => {
                        modal.classList.remove('is-active');
                        modals.active = null;
                        e.preventDefault();
                    });
                });

            // Handle modal success button click
            modal.querySelectorAll('button.modal-success-button')
                .forEach((btn: HTMLInputElement) => {
                    btn.addEventListener('click', e => {
                        const successCallback = modals.callbacks[modal.id]
                            || modals.callbacks['default'];
                        const modalResult = successCallback(modal);

                        if (modalResult && modalResult.success) {
                            if (update) {
                                update.value = modalResult.value;
                                update.dispatchEvent(new CustomEvent('input'));
                                update.dispatchEvent(new CustomEvent('change'));
                            }
                        }
                        if (!(modalResult) || modalResult.success) {
                            modal.classList.remove('is-active');
                            modals.active = null;
                        }

                        if (modalResult.success && btn.getAttribute('type') === 'submit' && btn.form) {
                            btn.form.requestSubmit();
                        }
                        e.preventDefault();
                    });
                });

            // Show modal
            launcher.addEventListener('click', e => {
                // Does the launcher have a condition?
                const condition = launcher.dataset.modalCondition;
                if (condition) {
                    if (!document.querySelector(condition)) {
                        return;
                    }
                }

                // Supply modal with source value
                const initialize = modals.initializers[modal.id]
                    || modals.initializers['default'];
                if (update) {
                    initialize(modal, update);
                }
                modal.classList.add('is-active');
                modals.active = modal;
                e.preventDefault();
            });
        });

    return modals;
}
