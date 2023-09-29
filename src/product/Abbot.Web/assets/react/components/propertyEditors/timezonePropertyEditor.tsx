import { useEffect } from "react";
import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DropDown} from "./formControls";

export interface Timezone {
    id: string,
    name: string,
}

export function TimezonePropertyEditor({ property, value, onChange }: StepPropertyProps<string>) {
    const tzJson = document.getElementById('timezone-json')?.textContent;
    const timezones = JSON.parse(tzJson || '[]');
    const tzOptions = timezones.map((tz: Timezone) => {
        return <option key={`channel-option-${tz.id}`} value={tz.id}>{tz.name}</option>
    });

    useEffect(() => {
        if (!value) {
            onChange(Intl.DateTimeFormat().resolvedOptions().timeZone);
        }
    }, [value]);

    const id = `${property.type}-${property.name}`

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property} htmlFor={id} />
            <DropDown id={id}
                name={property.name}
                onChange={onChange}
                required={!!property.required}
                value={value}>
                {!value && <option key=''></option> /* force picking a value if no default */}
                {tzOptions}
            </DropDown>
        </PropertyEditor>
    );
}