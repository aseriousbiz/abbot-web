import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = ["content"]
    declare readonly hasContentTarget: boolean;
    declare readonly contentTarget: HTMLElement;

    copy() {
        const target = this.hasContentTarget ? this.contentTarget : this.element;
        const content = (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) ? target.value : target.innerText;
        navigator.clipboard.writeText(content);
    }
}