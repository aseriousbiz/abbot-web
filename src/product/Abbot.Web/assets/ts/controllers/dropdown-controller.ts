import * as Turbo from "@hotwired/turbo";
import { Controller } from "@hotwired/stimulus";

export default class extends Controller<HTMLSelectElement> {
    navigateToSelectedValue() {
        if (this.element.value) {
            Turbo.visit(this.element.value);
        }
    }
}
