import Combobox from "@github/combobox-nav";
import { Controller } from "@hotwired/stimulus";
import { useClickOutside, useDebounce } from "stimulus-use";
import logger from "../log";
import { StimulusEvent } from "../stimulus";

const log = logger("autocomplete");

export default class extends Controller<HTMLElement> {
    static debounces = ["lookup"];
    static targets = ["input", "identifierInput", "results", "indicator"];
    static values = {
        "url": String
    };
    declare readonly inputTarget: HTMLInputElement;
    declare readonly resultsTarget: HTMLElement;
    declare readonly hasIndicatorTarget: boolean;
    declare readonly indicatorTarget: HTMLInputElement;
    declare readonly urlValue: string;

    combobox: Combobox;
    lastQuery?: string;
    open: boolean;

    connect() {
        // Debounce events to avoid spamming the server.
        useDebounce(this, {});

        // Handle clicking outside the results pane
        useClickOutside(this, { element: this.resultsTarget });
    }

    clickOutside(evt: StimulusEvent) {
        // If the popup isn't open, don't do anything
        if (!this.open) {
            return;
        }

        if (evt.target === this.inputTarget) {
            // Don't dismiss if the user clicked on the input
            return;
        }

        evt.preventDefault();
        this.dismiss();
    }

    clear() {
        this.inputTarget.value = "";
        this.dismiss();
    }

    dismiss() {
        this.open = false;
        this.resultsTarget.innerHTML = "";
        this.resultsTarget.classList.add("hidden");
    }

    async lookup() {
        // Read the current value from the input
        const value = this.inputTarget.value;

        if (value === this.lastQuery) {
            return;
        }

        if (!value || value.length === 0) {
            this.dismiss();
            return;
        }

        this.lastQuery = value;

        if (this.hasIndicatorTarget) {
            this.indicatorTarget.classList.remove("hidden");
        }

        // Make the query to the endpoint
        const url = new URL(this.urlValue, window.location.href);
        url.searchParams.set("q", value);

        log.log("Querying for results", { url: url.toString() });
        const resp = await fetch(url, {
            method: "GET",
        });
        const text = await resp.text();

        if (this.hasIndicatorTarget) {
            this.indicatorTarget.classList.add("hidden");
        }

        // Slap that text into the results element
        this.open = true;
        this.resultsTarget.innerHTML = text;
        this.resultsTarget.classList.remove("hidden");
    }

    comboboxTargetConnected(element: HTMLElement) {
        log.verbose("Connected combobox", element);
        this.combobox = new Combobox(this.inputTarget, element);
        this.combobox.start();
    }

    comboboxTargetDisconnected(element: HTMLElement) {
        log.verbose("Disconnected combobox", element);
        if (this.combobox) {
            this.combobox.stop();
            this.combobox.destroy();
        }
    }
}