/**
 * Updates the default value for every form element. This is useful when
 * using an Ajax form for example.
 *
 * @param {HTMLFormElement} form - The form to prompt for unsaved changes.
 */
export function updateDefaultValues(form) {
    for (const element of form.elements) {
        switch (element.type) {
            case "checkbox":
            case "radio":

            element.defaultChecked = element.checked;
            break;
        case "hidden":
        case "password":
        case "text":
        case "textarea":
        case "date":
        case "datetime-local":
        case "email":
        case "month":
        case "number":
        case "search":
        case "tel":
        case "time":
        case "url":
        case "week":
        case "color":
            element.defaultValue = element.value;
            break;
        case "select-one":
        case "select-multiple":
            for (const option of element.options) {
                option.defaultSelected = option.selected;
            }
        }
    }
}

