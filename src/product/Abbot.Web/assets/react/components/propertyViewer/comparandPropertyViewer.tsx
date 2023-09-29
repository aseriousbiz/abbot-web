import {PropertyViewerProps} from "./index";
import {PredefinedExpressions} from '../../models/predefinedExpressions';
import usePlaybook from "../../hooks/usePlaybooks";
import DefaultValueViewer from "./defaultValueViewer";

export default function ComparandPropertyViewer({ value, inputs, step }: PropertyViewerProps) {
    const { playbook } = usePlaybook();
    const predefinedExpression = inputs['left']; // The property name has to be hard-coded for now. Assumes continue-if.
    const daysInputExpression = PredefinedExpressions.getByName('customer_room_last_activity').value;

    if (location) {
        const pollOptions = playbook.findPrecedingPollOptions(step?.location);
        if (pollOptions) {
            const option = pollOptions.find(option => option.value === value);
            if (option) {
                return <>{option.label}</>
            }
        }
    }

    const displayValue = predefinedExpression === daysInputExpression
        ? `${value} days`
        : value;

    return (
        <DefaultValueViewer value={displayValue}/>
    );
}