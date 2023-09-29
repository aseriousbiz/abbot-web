import * as d3 from "d3";

export function addTooltips(chart, renderTooltip) {
    // Workaround if it's in a figure
    const type = d3.select(chart).node().tagName;
    let wrapper =
        type === "FIGURE" ? d3.select(chart).select<SVGElement>("svg") : d3.select(chart);

    // Workaround if there's a legend....
    const numSvgs = d3.select(chart).selectAll("svg").size();
    if (numSvgs === 2)
        wrapper = d3
            .select(chart)
            .selectAll("svg")
            .filter((d, i) => i === 1);
    wrapper.style("overflow", "visible"); // to avoid clipping at the edges

    // Set pointer events to visibleStroke if the fill is none (e.g., if its a line)
    wrapper.selectAll("path").each(function() {
        // For line charts, set the pointer events to be visible stroke
        if (
            d3.select(this).attr("fill") === null ||
            d3.select(this).attr("fill") === "none"
        ) {
            d3.select(this).style("pointer-events", "visibleStroke");
        }
    });

    const tip = wrapper
        .selectAll(".hover-tip")
        .data([""])
        .join("g")
        .attr("class", "hover")
        .attr("class", "shadow")
        .style("pointer-events", "none")
        .style("text-anchor", "middle");

    // Add a unique id to the chart for styling
    const id = id_generator();

    // Add the event listeners
    d3.select(chart)
        .classed(id, true) // using a class selector so that it doesn't overwrite the ID
        .selectAll("title")
        .each(function () {
            // Get the text out of the title, set it as an attribute on the parent, and remove it
            const title = d3.select(this); // title element that we want to remove
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const parent = d3.select((this as any).parentNode); // visual mark on the screen
            const t = title.text();
            if (t) {
                parent.attr("__title", t).classed("has-title", true);
                title.remove();
            }
            // Mouse events
            parent
                .on("mousemove", function (event) {
                    const text = d3.select(this).attr("__title");
                    const pointer = d3.pointer(event, wrapper.node());
                    if (text) tip.call(hover, pointer, text, renderTooltip);
                    else tip.selectAll("*").remove();

                    // Raise it
                    d3.select(this).raise();
                    // Keep within the parent horizontally
                    const tipSize = (tip.node() as SVGGraphicsElement).getBBox();
                    if (pointer[0] + tipSize.x < 0)
                        tip.attr(
                            "transform",
                            `translate(${tipSize.width / 2}, ${pointer[1] + 7})`
                        );
                    else if (pointer[0] + tipSize.width / 2 > (parseInt(wrapper.attr("width"))))
                        tip.attr(
                            "transform",
                            `translate(${parseInt(wrapper.attr("width")) - tipSize.width / 2}, ${
                                pointer[1] + 7
                            })`
                        );
                })
                .on("mouseout", function() {
                    tip.selectAll("*").remove();
                    // Lower it!
                    d3.select(this).lower();
                });
        });

    // Remove the tip if you tap on the wrapper (for mobile)
    wrapper.on("touchstart", () => tip.selectAll("*").remove());
    return chart;
}

const hover = (tip, pos, text, renderTooltip) => {
    const side_padding = 10;
    const vertical_padding = 10;
    const vertical_offset = 15;

    // Empty it out
    tip.selectAll("*").remove();

    if (text.startsWith('{') && renderTooltip) {
        // Deserialize the text
        const metrics = [JSON.parse(text)];

        tip.style("text-anchor", "middle")
            .style("pointer-events", "none")
            .selectAll("text")
            .data(metrics)
            .join(
                renderTooltip,
                update => update
            )
            .style("dominant-baseline", "ideographic");
    }
    else {
        text = text.split('\n');

        // Append the text as text and make the first line bold.
        tip
            .style("text-anchor", "middle")
            .style("pointer-events", "none")
            .selectAll("text")
            .data(text)
            .join("text")
            .style("dominant-baseline", "ideographic")
            .text((d) => d)
            .attr("y", (d, i) => (i - (text.length - 1)) * 15 - vertical_offset)
            .style("font-weight", (d, i) => (i === 0 ? "bold" : "normal"));
    }

    const bbox = tip.node().getBBox();

    // Add a rectangle (as background)
    tip
        .attr("transform", `translate(${pos[0] - bbox.width / 2}, ${pos[1] - bbox.height - 32})`)
        .append("rect")
        .attr("y", bbox.y - vertical_padding)
        .attr("x", bbox.x - side_padding)
        .attr("width", bbox.width + side_padding * 2)
        .attr("height", bbox.height + vertical_padding * 2)
        .style("fill", "white")
        .style("stroke", "#A1A1AA")
        .style("stroke-width", .5)
        .lower();
}

// To generate a unique ID for each chart so that they styles only apply to that chart
const id_generator = () => {
    const S4 = function () {
        return (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
    };
    return "a" + S4() + S4();
}
