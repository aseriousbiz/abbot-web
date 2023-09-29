import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = ["control", "showPasswordIcon", "hidePasswordIcon"];
    declare controlTarget: HTMLInputElement;
    declare hasControlTarget: boolean;
    declare showPasswordIconTarget: HTMLElement;
    declare hasShowPasswordIconTarget: boolean;
    declare hidePasswordIconTarget: HTMLElement;
    declare hasHidePasswordIconTarget: boolean;

    control: HTMLInputElement;

    connect() {
        if (this.hasControlTarget) {
            this.control = this.controlTarget;
        } else if (this.element instanceof HTMLInputElement) {
            this.control = this.element;
        } else {
            throw new Error("Must be either attached to a <input type='password'> or have the 'control' target point at an <input type='password'>");
        }

        this.render();
    }

    toggleVisibility() {
        if (this.control.type === "password") {
            this.control.type = "text";
        } else {
            this.control.type = "password";
        }

        this.render();
    }

    private render() {
        if (this.control.type === "password") {
            if (this.hasShowPasswordIconTarget) {
                this.showPasswordIconTarget.classList.remove("hidden");
            }
            if (this.hasHidePasswordIconTarget) {
                this.hidePasswordIconTarget.classList.add("hidden");
            }
        } else {
            if (this.hasShowPasswordIconTarget) {
                this.showPasswordIconTarget.classList.add("hidden");
            }
            if (this.hasHidePasswordIconTarget) {
                this.hidePasswordIconTarget.classList.remove("hidden");
            }
        }
    }
}
