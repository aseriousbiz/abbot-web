import * as Plot from "@observablehq/plot";
import {formatDate, getFullyQualifiedUrl} from "./charts";
import * as d3 from "d3";

/* Set up staff tools */
export function staffOnLoad() {
    const dauGraph = document.querySelector("#dau-graph") as HTMLElement;
    if (dauGraph) {
        const take = +dauGraph.dataset.take;
        const url = getFullyQualifiedUrl(`/staff/stats?data=dau&take=${take}`);
        attachDauGraph(url, take, dauGraph);
    }

    document.querySelectorAll('#queries tr>td>details').forEach((el: HTMLDetailsElement) => {
        const queryPre = el.querySelector('pre');
        if (!queryPre) {
            console.warn('Expected <pre> containing query text.');
            return;
        }

        // Selector ensures this structure
        const summaryRow = el.parentElement.parentElement as HTMLTableRowElement;

        const detailRow = document.createElement('tr');
        const detailCell = document.createElement('td');
        detailCell.colSpan = summaryRow.childElementCount;
        detailCell.appendChild(queryPre); // Move from <details> into its own row
        detailRow.style.display = 'none';
        detailRow.appendChild(detailCell);
        summaryRow.after(detailRow);

        el.addEventListener('toggle', event => {
            event.preventDefault();
            detailRow.style.display = el.open ? 'table-row' : 'none';
            return false;
        });
    });
}

async function attachDauGraph(dataUrl: URL, take: number, host: HTMLElement) {
    // Load data
    const result = await d3.csv(dataUrl.toString());

    // Get every column value
    const elements = Object.keys(result[0])
        .filter((d) => (d !== "date"));

    function render(metric: string) {
        const yDomain = [
            d3.min(result, (d) => +d[metric]),
            d3.max(result, (d) => +d[metric]) * 1.25
        ];

        const svg = Plot.plot({
            width: host.offsetWidth,
            height: host.offsetHeight,
            marginLeft: 70,
            marginBottom: 100,
            style: {
                fontSize: "14px",
                background: "transparent",
                color: "black",
            },
            x: {
                label: null,
                type: "band",
                tickFormat: d => formatDate(new Date(d)),
                tickRotate: 90,
            },
            y: {
                type: "linear",
                domain: yDomain,
                label: null,
                grid: true,
                width: 200,
            },
            marks: [
                Plot.barY(result, { x: "date", y: metric, fill: "rgb(32, 92, 136)" }),
                Plot.text(result, {
                    x: "date",
                    y: metric,
                    fill: "#3298dc",
                    fontSize: "14px",
                    dy: 15,
                    text: d => (+d[metric] != 0) ? `${d[metric]}` : null,
                }),
            ]
        });
        host.replaceChildren(svg);
    }

    d3.select("#take-selector")
        .on("change", function() {
            const val = (this as HTMLSelectElement).value;
            window.location.href = window.location.pathname + `?take=${val}`;
        });

    // Drop down for selecting the data to graph.
    d3.select("#stat-selector")
        .on("change", () => {
            const metric = (document.getElementById("stat-selector") as HTMLSelectElement).value;
            render(metric);
        })
        .selectAll("option")
        .data(elements)
        .enter()
        .append("option")
        .attr("value", (d) => d)
        .text((d) => d)

    render(elements[0]);
}