export function createIconSpan(classes: string): HTMLSpanElement {
    const i = document.createElement("i");
    i.className = classes;
    const span = document.createElement("span");
    span.appendChild(i);
    return span;
}