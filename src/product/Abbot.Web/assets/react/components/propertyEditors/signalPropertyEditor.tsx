import {TextInput} from "./formControls";
import {PropertyLabel, PropertyEditor, StepPropertyProps} from "./propertyEditor";

export function SignalPropertyEditor({ property, value, onChange }: StepPropertyProps<string>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <TextInput name={property.name}
                    onChange={onChange}
                    required={!!property.required}
                    value={value} />
            </PropertyLabel>
        </PropertyEditor>
    );
}