import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DropDown} from "./formControls";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import {useEffect} from "react";

export function ComparisonTypePropertyEditor({ property, value, onChange, inputs }: StepPropertyProps<string>) {
    // The set of comparison options depends on the current expression type.
    const predefinedExpression = inputs['left'] as string; // This has to be hard-coded for now. Assumes continue-if.
    const comparisonTypes = PredefinedExpressions.getAvailableComparisonTypes(predefinedExpression);
    const options = comparisonTypes.map((comparisonType) => (
        <option value={comparisonType.value} key={comparisonType.value} title={comparisonType.title}>{comparisonType.label}</option>
    ));

    useEffect(() => {
        if (value
            && comparisonTypes.length > 0
            && !comparisonTypes.find((comparisonType) => comparisonType.value === value)) {

            onChange(comparisonTypes[0].value);
        }
    },
    [value, onChange]);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <DropDown name={property.name}
                    onChange={onChange}
                    required={!!property.required}
                    value={value}>
                    {!value && <option key=''></option> /* force picking a value if no default */}
                    {options}
                </DropDown>
            </PropertyLabel>
        </PropertyEditor>
    );
}
