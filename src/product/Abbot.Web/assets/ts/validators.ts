import { StringKeyValuePair, ValidatableElement, ValidationService } from "aspnet-client-validation";

export default function registerProviders(validationService: ValidationService) {
    validationService.addProvider('requiredif', (value, element, params) => {
        if (value) {
            // If the value is a non-empty string, then it's valid.
            return true;
        }

        // Get the value this one depends on.
        const otherProperty = getDependsOnValue(element, params);

        // Must match RequiredIfAttribute.IsValid()
        // value missing (undefined) is *not* the same as required-if ""
        const isRequired = typeof params.value === 'undefined'
            ? otherProperty != ''
            : params.value.toString() === otherProperty;

        return !isRequired;
    });

    validationService.addProvider('greaterthan', (value: string, element: ValidatableElement, params: StringKeyValuePair) => {
        if (!value) {
            // If the value is null or an empty string, let the required validator handle it.
            return true;
        }

        // Get the value this one depends on.
        const dependsOnValue = getDependsOnValue(element, params);

        // Only Number is supported at the moment. We can add support for Date later.
        return Number(value) > Number(dependsOnValue);
    });

    function getDependsOnValue(element: ValidatableElement, params: StringKeyValuePair) {
        const dependsOn = params.dependson;
        const name = element.name;
        const prefix = name.substring(0, name.lastIndexOf(".") + 1);
        const dependsOnId = prefix + dependsOn;
        const dependsOnInputName = dependsOnId.replace(/[.[\]]/g, "_");
        return getValueFromFormElement(element.form[dependsOnInputName]);
    }

    function getValueFromFormElement(element): string {
        const type = (element[0] || element).type.toLowerCase();
        switch (type) {
            case "checkbox":
                return element[0].checked ? element[0].value : element[1].value;
            case "radio":
                return element.checked;
            default:
                return element.value;
        }
    }
}
