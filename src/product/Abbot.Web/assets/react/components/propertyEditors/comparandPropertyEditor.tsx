import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {PredefinedExpressions} from '../../models/predefinedExpressions';
import {ComparisonType} from "../../models/comparisonType";
import {Option} from "../../models/options";

export function ComparandPropertyEditor({ inputs, ...props }: StepPropertyProps<string | Option[]>) {
    const predefinedExpression = inputs['left'] as string; // This has to be hard-coded for now. Assumes continue-if.
    const comparisonType = inputs['comparison'] as ComparisonType;

    const handlebarsExpression = PredefinedExpressions.getByValueExpression(predefinedExpression);

    if (!predefinedExpression || !handlebarsExpression || comparisonType === 'Exists' || comparisonType === 'NotExists') {
        return null;
    }

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={props.property}>
                <handlebarsExpression.Input {...props} />
            </PropertyLabel>
        </PropertyEditor>
    );
}