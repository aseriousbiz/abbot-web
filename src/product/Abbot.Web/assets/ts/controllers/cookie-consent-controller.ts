import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLSelectElement> {
    static values = {
        cookie: String
    };
    declare cookieValue: string;

    accept() {
        document.cookie = this.cookieValue;
        this.element.style.display = 'none';
    }
}
