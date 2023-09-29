import * as d3 from "d3";

export function getDataUrlWithTimeZone(element: HTMLElement): URL {
    const dataUrl = element.dataset.url;
    if (!dataUrl) {
        throw new Error('data-url is not defined');
    }
    const url = getFullyQualifiedUrl(dataUrl);

    if (Intl && Intl.DateTimeFormat) {
        const browserTz = Intl.DateTimeFormat().resolvedOptions().timeZone;
        url.searchParams.set("tz", browserTz);
    }
    return url;
}

export function getFullyQualifiedUrl(path: string): URL {
    return new URL(path, window.location.toString());
}

export const formatDate = d3.timeFormat("%a %b %d");

const dayTicker = d => formatDate(d);
const monthTicker = d => d.getDay() === 1 // Monday
    ? dayTicker(d)
    : null;
const yearTicker = d => d.getDate() === 1
    ? dayTicker(d)
    : null;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function getTicker(data: any[]) {
    return data.length > 31
        ? yearTicker
        : data.length > 7
            ? monthTicker
            : dayTicker;
}
