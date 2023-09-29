import { Controller } from "@hotwired/stimulus";

/*
  Used to implement checkboxes that can be checked or unchecked as a group from a single "check all" checkbox.
  The check all checkbox also reflects the state of the checkboxes in the group such as all checked, some checked, or
  none checked.
 */
export default class extends Controller<HTMLElement> {
    static targets = [
        // The checkbox that toggles all the checkboxes in the group. If this is a button, clicking it selects all.
        "toggle",
        // A checkbox item in the group. There can be multiple.
        "item",
        // The action button or link that is disabled when no checkboxes are checked
        "action",
        // An element that displays the count of checked checkboxes
        "counter"
        ];
    static values = {
        "exclusive": Boolean
    };

    // The checkbox used to toggle all or none.
    declare toggleTarget: HTMLInputElement;

    // Whether the checkbox used to toggle all or none exists.
    declare hasToggleTarget: boolean;

    // The checkbox items
    declare itemTargets: HTMLInputElement[];

    // The elements (buttons, details, etc.) that take an action on checked items.
    // They are disabled when no checkboxes are checked
    declare actionTargets: HTMLElement[];

    // The element that displays the count of checked checkboxes
    declare counterTarget: HTMLElement;
    declare hasCounterTarget: boolean;

    // Whether any action targets exist.
    declare hasActionTarget: boolean;

    // Whether the checkboxes are exclusive to the toggle. If true, the toggle clears all the checkboxes, and any
    // checkbox checked clears the toggle. If false (default), the toggle checks all the checkboxes or unchecks them all.
    declare readonly exclusiveValue: boolean;

    // We have special behavior for a toggle that's a "select all" button.
    declare toggleIsSelectAllButton: boolean;

    connect() {
        this.update();
        this.toggleIsSelectAllButton = this.hasToggleTarget && this.toggleTarget instanceof HTMLButtonElement;
    }

    toggleTargetConnected(element: HTMLInputElement) {
        element.dataset.action ||= "checkboxes#toggle";
    }

    itemTargetConnected(element: HTMLInputElement) {
        element.dataset.action ||= "checkboxes#update";
    }

    toggle() {
        if (!this.hasToggleTarget) {
            return;
        }

        if (this.hasActionTarget) {
            this.actionTargets.forEach((target: HTMLElement) => this.disableActionTarget(target, !this.toggleIsSelectAllButton && !this.toggleTarget.checked));
        }

        this.itemTargets.forEach((checkbox: HTMLInputElement) => {
            if (!this.exclusiveValue) {
                // If the toggle is a button, it always selects all.
                checkbox.checked = this.toggleIsSelectAllButton || this.toggleTarget.checked;
            }
            else {
                if (this.toggleTarget.checked ) {
                    checkbox.checked = false;
                }
            }
        });

        this.updateCheckedCount();
    }

    update() {
        let allChecked = true;
        let allUnchecked = true;

        this.itemTargets.forEach((checkbox) => {
            allUnchecked &&= !checkbox.checked;
            allChecked &&= checkbox.checked;
        });
        this.updateCheckedCount();

        if (this.hasActionTarget) {
            this.actionTargets.forEach(target => this.disableActionTarget(target, allUnchecked));
        }

        if (!this.hasToggleTarget) {
            return;
        }

        if (this.exclusiveValue) {
            this.toggleTarget.checked = allUnchecked;
        } else {
            // Handles updating the toggle checkbox to reflect the state of all the item checkboxes.
            if (allChecked) { // All checked
                this.toggleTarget.indeterminate = null;
                this.toggleTarget.checked = true;
            } else if (allUnchecked) { // All unchecked
                this.toggleTarget.indeterminate = null;
                this.toggleTarget.checked = false;
            } else {
                this.toggleTarget.indeterminate = true;
                this.toggleTarget.checked = false;
            }
        }
    }

    updateCheckedCount(): void {
        if (this.hasCounterTarget) {
            const checkedCount: number = this.itemTargets.filter((checkbox) => checkbox.checked).length;
            this.counterTarget.innerText = `${checkedCount}`;
        }
    }

    disableActionTarget(target: HTMLElement, disabled: boolean) {
        if (target instanceof HTMLInputElement) {
            target.disabled = disabled;
        } else if (target instanceof HTMLButtonElement) {
            // .btn[disabled] is styled correctly
            target.disabled = disabled;
        } else {
            if (disabled) {
                target.classList.add('disabled');
            } else {
                target.classList.remove('disabled');
            }

            const btn = target.matches('.btn') ? target : target.querySelector('.btn');
            if (btn) {
                if (disabled) {
                    btn.classList.add('btn-disabled');
                }
                else {
                    btn.classList.remove('btn-disabled');
                }
            }
        }
    }
}
