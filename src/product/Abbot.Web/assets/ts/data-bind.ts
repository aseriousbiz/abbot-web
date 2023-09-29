/*
 * Given a target element and a value, gets the value of the target element.
 */
export function getValue(target: HTMLElement) : string {
    if (target instanceof HTMLInputElement) {
        if (target.type === 'checkbox' || target.type === 'radio') {
            return target.checked ? 'true' : 'false';
        }
        return target.value;
    }
    else if (target instanceof HTMLSelectElement
        || target instanceof HTMLTextAreaElement
        || target instanceof RadioNodeList) {
        return target.value;
    }
}

/*
 * Given a target element and a value, sets the value of the target element.
 */
export function setValue(target: HTMLElement, value: string) {
    if (target instanceof HTMLInputElement) {
        if (target.type === 'checkbox' || target.type === 'radio') {
            target.checked = value === 'true';
        }
        else {
            target.value = value;
        }
    }
    else if (target instanceof HTMLSelectElement
        || target instanceof HTMLTextAreaElement
        || target instanceof RadioNodeList) {
        target.value = value;
    }
}

/*
 * Given a set of elements, returns a hash of their keys and values. Keys are determined by the
 * data-bind-key attribute. Values are determined by the element type. See `getBindKey` and `getValue`
 * for more details.
 */
export function getValuesHash(elements: HTMLElement[]): Map<string, string> {
    return elements.reduce((acc, element) => acc.set(getBindKey(element), getValue(element)), new Map<string, string>());
}

/*
 * When binding elements to key value pairs (e.g. a hash), this function returns the key to bind the target
 * element to. Right now, this is the value of the `data-bind-key` attribute.
 */
export function getBindKey(target: HTMLElement) : string {
    return target.dataset.bindKey;
}
