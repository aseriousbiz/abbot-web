/*
  ajax-partial.js

  Populates the content of an element in a page from a URL.
 */
export async function ajaxPartialOnLoad() {
  const ajaxElements = document.querySelectorAll("[data-partial-url]");
  for (const element of ajaxElements) {
    if (element instanceof HTMLElement) {
      const location = window.location;
      const host = `${location.protocol}//${location.host}`;
      const url = new URL(element.dataset.partialUrl, host);
      if (element.dataset.appendTimezone) {
        const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
        url.searchParams.set('tz', timezone);
      }
      const response = await fetch(url.href);
      element.innerHTML = await response.text();
    }
  }
}
