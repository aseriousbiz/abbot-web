import { Controller } from "@hotwired/stimulus";
import * as Viz from "@viz-js/viz";

type VizResult = {
    status: "success" | "failure",
    output: string,
    errors: Array<{ level?: "error" | "warning", message: string }>
};

export default class extends Controller<HTMLElement> {
    static targets = ["source"];
    declare sourceTarget: HTMLScriptElement;

    async connect() {
        const source = this.sourceTarget.innerHTML;
        const viz = await Viz.instance();
        console.log("Version", viz.graphvizVersion);
        console.log("Engines", viz.engines);
        console.log("Formats", viz.formats);
        const result: VizResult = viz.render(source, { format: "svg", engine: "dot" });

        if (result.errors.length > 0) {
            console.error("Graphviz errors:", result.errors);
            return;
        }

        // Add the SVG to the DOM
        this.element.innerHTML += result.output;

        // Add the '.graphviz' class to the SVG
        const svg = this.element.querySelector("svg");
        if (svg) {
            svg.classList.add("graphviz");
        }
    }
}
