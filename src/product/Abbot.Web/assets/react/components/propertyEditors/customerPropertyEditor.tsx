import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DropDown, ExpressionSelect} from "./formControls";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import * as React from "react";
import {useEffect, useMemo} from "react";
import usePlaybook from "../../hooks/usePlaybooks";

export function CustomerPropertyEditor({step, ...props}: StepPropertyProps<string>) {
    // Special case for now. We can clean it up later.
    if (step.type.name === 'system.create-customer') {
        return (
            <CustomerNamePropertyEditor {...props} step={step} />
        );
    }
    return (
        <CustomerIdPropertyEditor {...props} step={step} />
    );
}

const customerExpressions = [
    PredefinedExpressions.getByName("customer"),
];

const webhookPayloadExample = `{
 "customer": {
    "name": "Customer Name"
  },
 ...
}`;

export function CustomerIdPropertyEditor({ property, value, onChange, step }: StepPropertyProps<string>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property} className="flex flex-col">
                <ExpressionSelect
                    step={step}
                    property={property}
                    onChange={onChange}
                    value={value}
                    expressions={customerExpressions} />
            </PropertyLabel>
        </PropertyEditor>
    );
}

const customerNameExpression = PredefinedExpressions.getByName("customer_name");

export function CustomerNamePropertyEditor({ property, value, onChange, step }: StepPropertyProps<string>) {
    const { playbook } = usePlaybook();

    // Single source of truth for this default lives in JS
    useEffect(() => {
        if (!value) {
            onChange(customerNameExpression.value)
        }
    }, [value]);

    const presentation = useMemo(
        () => {
            const sourceStep = customerNameExpression.getSourceStep(playbook, step);
            return customerNameExpression.getPresentation(sourceStep);
        },
        [playbook]);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property} className="flex flex-col">
                <DropDown name={property.name}
                          onChange={onChange}
                          required={!!property.required}
                          value={value}>
                    <option value={customerNameExpression.value}>{presentation.label}</option>
                </DropDown>
            </PropertyLabel>
            <div className="font-normal text-xs text-gray-500">
                Example:
                <pre><code className="text-xs">{webhookPayloadExample}</code></pre>
            </div>
        </PropertyEditor>
    );
}