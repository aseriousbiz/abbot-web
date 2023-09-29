import { useCallback, useEffect } from "react";
import { PropertyEditor, PropertyLabel, StepPropertyProps } from "./propertyEditor";
import { DropDown } from "./formControls";
import { OptionsDefinition, findPresets } from "../../models/options";

export function OptionsPropertyEditor({ property, value, onChange }: StepPropertyProps<OptionsDefinition>) {
    const optionsPresets = findPresets(property.type?.hint);

    const handlePreset = useCallback((preset: string) => {
        const optionsPreset = optionsPresets.find(p => p.preset === preset);
        onChange(optionsPreset);
    }, [optionsPresets]);

    useEffect(() => {
        if (typeof value === 'string') {
            handlePreset(value);
        }
    }, [value])


    const options = optionsPresets.map(preset => (
        <option value={preset.preset} key={preset.preset}>{preset.name}</option>
    ));
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <DropDown name={property.name}
                    onChange={handlePreset}
                    required={!!property.required}
                    value={value?.preset}>
                    {!value && <option key=''></option> /* force picking a value if no default */}
                    {options}
                </DropDown>
            </PropertyLabel>
        </PropertyEditor>
    );
}