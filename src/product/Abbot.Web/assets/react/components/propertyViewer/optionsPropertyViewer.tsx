import React from "react";
import { PropertyViewerProps } from ".";
import { isStaffMode } from "../../../ts/env";
import { OptionsDefinition, findPresets } from "../../models/options";

export default function OptionsPropertyViewer({ property, value }: PropertyViewerProps) {
    if (typeof value !== 'string' && typeof value !== 'object') {
        throw new Error(`Expected options value to be a string or object; received ${typeof value}`);
    }
    if (typeof value === 'string') {
        value = findPresets(property.type?.hint).find(p => p.preset === value);
        if (!value) {
            throw new Error(`Preset options ${value} not found`);
        }
    }
    if (typeof value !== 'object') {
        return null;
    }
    const optionsPreset = value as OptionsDefinition;

    return (
        <details onClick={e => e.stopPropagation()}>
            <summary>
                {optionsPreset.name}
                {isStaffMode && <code>{optionsPreset.preset}</code>}
            </summary>
            <dl className="grid grid-cols-[1fr_min-content] gap-2">
                <React.Fragment key=''>
                    <dd className="font-semibold">Label</dd>
                    <dt className="font-semibold">Value</dt>
                </React.Fragment>
                {optionsPreset.options.map(o => (
                    <React.Fragment key={o.value}>
                        <dd>{o.label}</dd>
                        <dt><code className='text-xs'>{o.value}</code></dt>
                    </React.Fragment>
                ))}
            </dl>
        </details>
    )
}