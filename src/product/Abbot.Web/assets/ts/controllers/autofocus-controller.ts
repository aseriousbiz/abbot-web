import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";

@autobind("input[autofocus]")
export default class extends Controller<HTMLInputElement> {
    connect() {
        this.element.focus()
    }
}
