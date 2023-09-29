import { Controller } from "@hotwired/stimulus";
import logger from "../log";

const log = logger("interval");

export default class extends Controller<Element> {
    static values = {
        interval: Number,
    };
    declare intervalValue: number;
    intervalHandle: NodeJS.Timer;

    connect() {
        this.intervalHandle = setInterval(() => {
            log.verbose("interval elapsed");
            this.dispatch("tick");
        }, this.intervalValue);
    }

    disconnect(): void {
        clearInterval(this.intervalHandle);
    }
}
