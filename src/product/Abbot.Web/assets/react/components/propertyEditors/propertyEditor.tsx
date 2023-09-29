import * as React from "react";
import { Label, TextInput } from "./formControls";
import { isStaffMode } from "../../../ts/env";
import { Step, StepProperty } from "../../models/step";
import {useEffect} from "react";
import {HandlebarExpression} from "../../models/predefinedExpressions";

export interface StepPropertyProps<StorageType> {
    step: Step,
    property: StepProperty,
    onChange(value: StorageType): void,
    value?: StorageType,
    inputs?: Record<string, unknown>, // Some properties need to know about other inputs
}

export interface StepPropertyWithExpressionsProps<StorageType> extends StepPropertyProps<StorageType> {
    expressions: HandlebarExpression[],
}

export interface PropertyLabelProps extends React.PropsWithChildren<React.HTMLProps<HTMLLabelElement>> {
    stepProperty: StepProperty,
    hideDescription?: boolean,
}

export function PropertyEditor(props: {children: React.ReactNode}) {
    return (
        <div className="flex flex-col gap-y-1 my-2">
            {props.children}
        </div>
    );
}

export function PropertyLabel({ stepProperty, children, hideDescription, ...props }: PropertyLabelProps) {
    return <Label className="flex flex-col" {...props} aria-label={stepProperty.description}>
        <div>
            {stepProperty.title}
            {stepProperty.hidden && isStaffMode && (
                <span className="ml-1" data-tooltip="Editable by Abbot Staff only.">
                    <i className="fa-duotone fa-shield-check"></i>
                </span>
            )}
        </div>
        
        {children}
        
        {!hideDescription && stepProperty.description && (
            <p className="text-xs font-normal text-slate-500">
                {stepProperty.description}
            </p>
        )}
    </Label>
}

export function StringPropertyEditor({ property, value, onChange }: StepPropertyProps<string>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <TextInput type="text"
                    placeholder={property.placeholder}
                    name={property.name}
                    onChange={onChange}
                    required={!!property.required}
                    value={value} />
            </PropertyLabel>
        </PropertyEditor>
    );
}

export function FloatPropertyEditor({ property, value, onChange }: StepPropertyProps<number>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <TextInput type="number" step="any"
                    name={property.name}
                    onChange={v => onChange(parseFloat(v))}
                    required={!!property.required}
                    value={value} />
            </PropertyLabel>
        </PropertyEditor>
    );
}

export function IntegerPropertyEditor({ property, value, onChange }: StepPropertyProps<number>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <TextInput type="number" step={1}
                    name={property.name}
                    onChange={v => onChange(parseInt(v))}
                    required={!!property.required}
                    value={value} />
            </PropertyLabel>
        </PropertyEditor>
    );
}

export function BooleanPropertyEditor({ property, value, onChange }: StepPropertyProps<boolean>) {
    // Ignore 'required'. It makes the checkbox a "You must check this to proceed" box and that's not really relevant to our scenario.
    useEffect(() => {
        if (value === undefined) {
            onChange(false);
        }
    }, []);

    return (
        <PropertyEditor>
            <div className="flex flex-col">
                <PropertyLabel stepProperty={property} className="flex flex-row" hideDescription={true}>
                    <input type="checkbox"
                        className="-order-1 mr-1"
                        name={property.name}
                        onChange={e => onChange(e.currentTarget.checked)}
                        defaultChecked={value} />
                </PropertyLabel>
                {property.description && (
                    <p className="text-xs font-normal text-slate-500">
                        {property.description}
                    </p>)}
            </div>
        </PropertyEditor>
    );
}

