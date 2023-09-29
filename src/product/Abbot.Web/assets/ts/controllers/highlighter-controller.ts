import { Controller } from "@hotwired/stimulus";
import logger from "../log";
import { StimulusEvent } from "../stimulus";

const log = logger("highlighter");

export default class extends Controller<HTMLElement> {
    static targets = ["item"];
    declare itemTargets: HTMLElement[];

    static classes = ["highlighted"];
    declare hasHighlightedClass: boolean;
    declare highlightedClass: string;

    highlight(evt: StimulusEvent<{ ids: string }>) {
        log.verbose('Highlighting elements', { elementIds: evt.params.ids });
        const targets = evt.params.ids.split(",");
        this.itemTargets.forEach(item => {
            const idStr = item.getAttribute("data-item-id");
            if (idStr && idStr.length > 0) {
                const ids = idStr.split(",");
                if (targets.find(t => ids.find(i => t === i))) {
                    item.classList.add(this.highlightedClass);
                } else {
                    item.classList.remove(this.highlightedClass);
                }
            }
        })
    }

    unhighlight() {
        log.verbose("Unhighlighting all");
        this.itemTargets.forEach(item => {
            item.classList.remove(this.highlightedClass);
        })
    }
}