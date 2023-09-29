import * as d3 from "d3";
import * as Plot from "@observablehq/plot";
import { addTooltips } from "./plot-tooltip";
import { ConversationVolume } from "./../api/internal";
import { getDataUrlWithTimeZone, getTicker, formatDate } from "./charts";

export function insightsOnLoad() {
    const conversationVolumeGraph = document.querySelector("#conversation-volume-graph") as HTMLElement;
    if (conversationVolumeGraph) {
        const url = getDataUrlWithTimeZone(conversationVolumeGraph)

        // We don't await this on purpose so the rest of the code can continue to execute
        // while loading the graph.
        attachConversationsVolumeGraph(url, conversationVolumeGraph);
    }
}

async function attachConversationsVolumeGraph(dataUrl: URL, host: HTMLElement) {
    const resp = await fetch(dataUrl.href);
    const result: ConversationVolume = await resp.json();

    const hasData = result.data.length > 0;
    if (!hasData) {
        const loading = host.querySelector(".loading");
        if (loading instanceof HTMLElement) {
            loading.style.display = 'none';
        }

        const noData = host.querySelector(".no-data");
        if (noData instanceof HTMLElement) {
            noData.style.display = 'block';
        }
        return;
    }

    const data = result.data.map(d => {
        const item = ({
            date: new Date(d.date),
            overdue: (+d.overdue || 0),
            overdue_start: 0,
            new: (+d.new || 0),
            open: (+d.open || 0),
        });
        if (item.overdue > item.new) {
            item.overdue_start = item.new;
        }
        return item;
    });

    function render() {
        // Compute the domain for the y-axis.
        // By default, Plot uses [min, max], but we want to put a little head-room on top so we set the domain to [min, max * 1.25].
        const yDomain = [
            0,
            Math.max(10, d3.max(data, d => Math.max(d.open, d.new, d.overdue)) * 1.25)
        ];

        const ticker = getTicker(data);

        const svg = Plot.plot({
            width: 1294,
            height: 368,
            marginLeft: 70,
            marginBottom: 40,
            style: {
                fontSize: "14px",
            },
            x: {
                label: null,
                type: "band",
                tickSize: 0,
                tickPadding: 15,
                tickFormat: d => ticker(new Date(d)),
            },
            y: {
                tickSize: 0,
                label: null,
                domain: yDomain,
                grid: true,
                tickFormat: d => d,
                width: 200,
            },
            marks: [
                Plot.barY(data, { x: "date", y: "open", fill: "#E0E7FF", title: JSON.stringify }),
                Plot.barY(data, { x: "date", y: "new", fill: "#A5B4FC", title: JSON.stringify }),
                Plot.barY(data, { x: "date", y: "overdue", y1: "overdue_start", fill: "#6366F1", title: JSON.stringify }),
                Plot.ruleY([0]),
                Plot.text(data, {
                    x: "date",
                    y: "value",
                    fill: '#818CF8',
                    fontSize: "14px",
                    dy: -15,
                    text: d => {
                        return d.value;
                    }
                }),
            ]
        });
        host.replaceChildren(svg);
        addTooltips(svg, renderTooltip);
    }

    function renderTooltip(tooltip) {
        const initialOffset = 20;
        const circleRowOffset = 18;
        const boxWidth = 200;

        let rowCount = 0;

        const appendDateHeader = () => {
            tooltip.append("text")
                .text(d => formatDate(new Date(d.date)))
                .attr("text-anchor", "start")
                .attr("font-weight", "bold")
                .attr("y", initialOffset);
        }

        const appendTotal = (metric, value) => {
            const verticalOffset = initialOffset + circleRowOffset;
            tooltip.append("text")
                .text(metric)
                .attr("text-anchor", "start")
                .attr("x", 0)
                .attr("y", verticalOffset);

            tooltip.append("text")
                .text(value)
                .attr("x", boxWidth)
                .attr("y", verticalOffset);

            rowCount++;
        }

        const appendCircleRow = (metric, value, color) => {
            const verticalOffset = initialOffset + (circleRowOffset * ++rowCount);
            tooltip.append("circle")
                .attr("cx", 5)
                .attr("cy", verticalOffset)
                .attr("r", 5)
                .attr("fill", color);

            tooltip.append("text")
                .text(metric)
                .attr("text-anchor", "start")
                .attr("x", 15)
                .attr("y", verticalOffset + 5);

            tooltip.append("text")
                .text(value)
                .attr("x", boxWidth)
                .attr("y", verticalOffset + 5);
        };

        appendDateHeader()
        appendTotal("Open Conversations", d => d.open);
        appendCircleRow("New", d => d.new, "#A5B4FC");
        appendCircleRow("Overdue", d => d.overdue, "#6366F1");
    }

    render();
}
