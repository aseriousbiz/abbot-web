import {PropertyViewerProps} from "./index";
import DefaultValueViewer from "./defaultValueViewer";
import {PredefinedExpressions} from "../../models/predefinedExpressions";

export function ComparisonTypePropertyViewer({value, inputs}: PropertyViewerProps) {
    const predefinedExpression = inputs['left'] as string; // This has to be hard-coded for now. Assumes continue-if.
    const comparisonTypes = PredefinedExpressions.getAvailableComparisonTypes(predefinedExpression);
    const comparisonType = comparisonTypes.find(ct => ct.value === value);

    if (!comparisonType) {
        return (<>Deprecated comparison type</>);
    }

    return (
        <>
            <DefaultValueViewer value={comparisonType.label}/>
            {comparisonType.title && <div className="italic text-xs text-gray-700 font-normal">{comparisonType.title}</div>}
        </>

    );
}