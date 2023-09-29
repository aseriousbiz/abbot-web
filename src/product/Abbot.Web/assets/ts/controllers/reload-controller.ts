import { Controller } from "@hotwired/stimulus";
import { FrameElement } from "@hotwired/turbo";
import logger from "../log";

const log = logger("reload");

/*
 * When applied to a turbo-frame that has a `src` attribute, this controller will reload the frame every `timeout` ms.
 */
export default class extends Controller {
    static values = {
        interval: Number // Timeout in ms
    }

    declare document: Document;
    declare interval;
    declare intervalValue: number;
    declare frame: FrameElement;

    initialize() {
        this.intervalValue ||= 5000;
        this.frame = this.element as FrameElement;
        this.document = document;
    }

    reload() {
        this.updateSrc(true);
    }

    connect() {
        if (this.intervalValue > 0) {
            this.startInterval();
            this.document.addEventListener('visibilitychange', this.handleVisibilityChange);
            window.addEventListener('focus', this.handleFocus);
        }
    }

    disconnect() {
        this.disconnectInterval();
        this.document.removeEventListener('visibilitychange', this.handleVisibilityChange);
        window.removeEventListener('focus', this.handleFocus);
    }

    private startInterval() {
        if (this.interval) {
            this.disconnectInterval();
        }
        if (this.frame && this.frame.src && this.intervalValue > 0) {
            this.interval = setInterval(() => this.updateSrc(false), this.intervalValue);
        }
    }

    private disconnectInterval() {
        clearInterval(this.interval);
    }

    private updateSrc(force: boolean) {
        if (!this.frame || !this.frame.src || !document.hasFocus() || document.hidden) {
            this.disconnectInterval();
            if (!force) {
                log.verbose("Not reloading frame because it's not visible or the document doesn't have focus.", {frame: this.frame.id});
                return;
            }
        }

        // Test if the activeElement is inside a form.
        // If so, we don't want to reload the frame, because the user is probably typing something.
        const activeElement = document.activeElement;
        if (activeElement && !this.canReload(activeElement)) {
            log.verbose("Not reloading frame because the active element is in a form or a checkbox is checked that prevents reload.", {frame: this.frame.id});
            this.disconnectInterval();
            const blurCallback = () => {
                activeElement.removeEventListener('blur', blurCallback);
                this.startInterval();
            };
            activeElement.addEventListener('blur', blurCallback);
            return;
        }

        log.verbose("Reloading frame", {frame: this.frame.id});
        this.frame.reload();
    }

    canReload(activeElement: Element): boolean {
        if (activeElement && activeElement.closest("form")) {
            return false;
        }
        const elements = this.frame.querySelectorAll(".prevent-reload-when-checked:checked");
        return elements.length === 0;
    }

    handleVisibilityChange = () => {
        if (document.hidden) {
            this.disconnectInterval();
        } else {
            this.startInterval();
        }
    };

    handleFocus = () => {
        this.startInterval();
    };
}
