import { Controller } from "@hotwired/stimulus";
import { autobind } from "../autocontrollers";

function getCurrentOS() {
    // The 'userAgentData' property isn't in TypeScript's definition of navigator yet.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const anyNav = navigator as any;
    if (anyNav.userAgentData?.platform) {
        return anyNav.userAgentData.platform.toLowerCase();
    } else {
        if (window.navigator.platform.startsWith("Mac")) {
            return "macOS";
        } else if (window.navigator.platform.startsWith("Win")) {
            return "Windows";
        } else {
            return "Other";
        }
    }
}

@autobind("[data-os]")
export default class extends Controller<HTMLElement> {
    connect() {
        const requiredOs = this.element.dataset.os;
        const currentOs = getCurrentOS();
        if (requiredOs.toLowerCase() !== currentOs.toLowerCase()) {
            this.element.remove();
        } else {
            this.element.classList.remove("hidden");
        }
    }
}
