import * as d3 from "d3";
import * as Plot from "@observablehq/plot";
import * as moment from "moment";
import { addTooltips } from "./plot-tooltip";
import { getDataUrlWithTimeZone, getTicker } from "./charts";

import { Trends } from "../api/internal"

const metrics = [
  "averageTimeToFirstResponse",
  "averageTimeToResponse",
  "averageTimeToFirstResponseDuringCoverage",
  "averageTimeToResponseDuringCoverage",
  "averageTimeToClose",
  "newConversations"] as const;
type Metric = typeof metrics[number];

export async function conversationChartsOnLoad() {
  const trendsGraph = document.querySelector("#trends-graph") as HTMLElement;
  if (trendsGraph) {
    const metricSelectors = document.querySelectorAll("li[data-trends-metric]");
    const url = getDataUrlWithTimeZone(trendsGraph)
    const resp = await fetch(url.toString());
    const trends: Trends = await resp.json();

    setInnerText(trendsGraph.dataset.ttfr, formatDuration(trends.summary.averageTimeToFirstResponse));
    setInnerText(trendsGraph.dataset.ttr, formatDuration(trends.summary.averageTimeToResponse));
    setInnerText(trendsGraph.dataset.ttfrdc, formatDuration(trends.summary.averageTimeToFirstResponseDuringCoverage));
    setInnerText(trendsGraph.dataset.ttrdc, formatDuration(trends.summary.averageTimeToResponseDuringCoverage));

    attachTrendsGraph(trends, trendsGraph, metricSelectors as NodeListOf<HTMLElement>);
  }
}

function setInnerText(id: string, value: string) {
  const el = document.getElementById(id);
  if (el) {
    el.innerText = value;
  }
}

function quantity(value: number, unit: string) {
  return value === 1 ? `1 ${unit}` : `${value} ${unit}s`;
}

function formatDuration(duration: number): string | undefined {
    if (duration > 0) {
        const span = moment.duration(duration, 'seconds');
        return span.days() > 0
            ? `${quantity(span.days(), "day")} ${quantity(span.hours(), "hour")}`
            : span.hours() > 0
                ? `${quantity(span.hours(), "hour")} ${quantity(span.minutes(), "minute")}`
                : span.minutes() > 0
                    ? `${quantity(span.minutes(), "minute")} ${quantity(span.seconds(), "second")}`
                    : `${quantity(span.seconds(), "second")}`;
    }
    else {
        return "0";
    }
}

function formatShortDuration(duration: number): string | undefined {
    if (duration > 0) {
        const span = moment.duration(duration, 'seconds');
        return span.days() > 0
            ? `${span.days()}d ${span.hours()}h`
            : span.hours() > 0
                ? `${span.hours()}h ${span.minutes()}m`
                : span.minutes() > 0
                    ? `${span.minutes()}m ${span.seconds()}s`
                    : `${span.seconds()}s`;
    }
    else {
        return "0";
    }
}

async function attachTrendsGraph(trends: Trends, host: HTMLElement, metricSelectors: NodeListOf<HTMLElement>) {
    const data = trends.data.map((d) => ({
        date: new Date(d.date),
        averageTimeToFirstResponse: +(d.averageTimeToFirstResponse || 0),
        averageTimeToResponse: +(d.averageTimeToResponse || 0),
        averageTimeToFirstResponseDuringCoverage: +(d.averageTimeToFirstResponseDuringCoverage || 0),
        averageTimeToResponseDuringCoverage: +(d.averageTimeToResponseDuringCoverage || 0),
        averageTimeToClose: +(d.averageTimeToClose || 0),
        newConversations: +(d.newConversations || 0),
        percentWithinTarget: +(d.percentWithinTarget ?? -1),
    }));

  function render(metric: Metric) {
    // Compute the domain for the y-axis.
    // By default, Plot uses [min, max], but we want to put a little head-room on top so we set the domain to [min, max * 1.25].
    const yDomain = [
      d3.min(data, (d) => d[metric]),
      d3.max(data, (d) => d[metric]) * 1.25
    ];

    const ticker = getTicker(data);
    const showYAxis = host.dataset.showYAxis === "true";

    const svg = Plot.plot({
      width: host.offsetWidth,
      height: host.offsetHeight,
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
        tickFormat: d => {
          if (metric === "newConversations") {
            return d;
          }

          if (!showYAxis && d < 3600) {
            return;
          }
          return formatShortDuration(d);
        },
        width: 200,
      },
      marks: [
        Plot.line(data, { x: "date", y: metric, stroke: '#818CF8' }),
        Plot.dot(data, { x: "date", y: metric, fill: '#818CF8', r: 5, title: JSON.stringify }),
        Plot.text(data, {
          x: "date",
          y: metric,
          fill: '#818CF8',
          fontSize: "14px",
          dy: -15,
          text: d => {
            if (metric == "newConversations") {
              return d.newConversations > 0 ? d.newConversations : null;
            } else {
              return showYAxis ? null : formatDuration(d[metric]);
            }
          }
        }),
      ]
    }) as SVGElement;
    host.replaceChildren(svg);
    addTooltips(svg, tooltip => renderTooltip(tooltip, metric));
  }

  // Read the initial metric selection from the fragment or the data attribute.
  let initialMetric = window.location.hash.substring(1) as Metric
    || host.dataset.initialMetric as Metric
    || "averageTimeToResponse";

  if (metrics.indexOf(initialMetric) === -1) {
    initialMetric = "averageTimeToResponse";
  }

  metricSelectors.forEach((selector) => {
    const metric = selector.dataset.trendsMetric;
    if (metric == initialMetric) {
      selector.classList.add("bg-indigo-100");
    }

    const latest = selector.querySelector("[data-trends-metric-latest]");
    if (latest) {
      const avg = trends.summary[metric];
      let value = avg.toString();
      if (metric !== "newConversations") {
        value = formatDuration(avg);
      }
      latest.textContent = value;
    }

    selector.addEventListener("click", (evt) => {
      evt.preventDefault();
      window.history.pushState(null, null, `#${metric}`);

      metricSelectors.forEach((s) => {
        s.dataset.trendsMetricSelected = undefined
        s.classList.remove("bg-indigo-100")
      });
      selector.dataset.trendsMetricSelected = "true";
      selector.classList.add("bg-indigo-100");
      render(metric as Metric);
    });
  });

  function renderTooltip(tooltip, metric) {
    const formatValue = (value: number) => {
      if (metric !== "newConversations" && value === 0) {
        return "No data";
      }
      if (metric === "newConversations") {
        return `${value} conversation${value === 1 ? "" : "s"}`;
      }
      return formatDuration(value);
    };

    const formatTargetPercentage = (value: number) => value >= 0 ? ` (${value}% within target)` : "";

    tooltip.append("text")
      .text(d => formatValue(d[metric]))
      .attr("fill", "#818CF8")
      .attr("font-weight", "bold")
      .attr("x", 0)
      .attr("y", 20)
      .append("tspan")
      .attr("font-weight", "normal")
      .text(d => formatTargetPercentage(d["percentWithinTarget"]));
  }

  render(initialMetric);
}
