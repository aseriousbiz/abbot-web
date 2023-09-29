import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {ExpressionSelect} from "./formControls";

import {PredefinedExpressions} from '../../models/predefinedExpressions';

export function PredefinedExpressionPropertyEditor({ step, property, value, onChange }: StepPropertyProps<string>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <ExpressionSelect
                    property={property}
                    step={step}
                    onChange={onChange}
                    value={value}
                    expressions={PredefinedExpressions.all} />
            </PropertyLabel>
        </PropertyEditor>
    );
}