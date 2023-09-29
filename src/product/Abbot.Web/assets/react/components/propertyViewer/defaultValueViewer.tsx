import {PredefinedExpressions} from "../../models/predefinedExpressions";
import {Option} from "../../models/options";
import usePlaybook from "../../hooks/usePlaybooks";
import {PlacedStep} from "../../models/step";

export default function DefaultValueViewer(props: { value: unknown, step?: PlacedStep }) {
    const { playbook } = usePlaybook();
    const { value, step } = props;

    function formatArrayValue(item: unknown, index: number) {
        if (typeof item === 'string') {
            return (
                <span key={item}
                      className="bg-gray-100 mr-1 px-1 rounded has-tooltip-arrow"
                      data-tooltip-id="global-tooltip"
                      data-tooltip-content={item}>
                    {item}
                    {index < (value as Option[]).length - 1 && ', '}
                </span>
            );
        }

        if ((item as Option).value) {
            const option = item as Option;
            return (
                <span key={option.value}
                      className="bg-gray-100 mr-1 px-1 rounded has-tooltip-arrow"
                      data-tooltip-id="global-tooltip"
                      data-tooltip-content={option.value}>
                    {option.label}
                    {index < (value as Option[]).length - 1 && ', '}
                </span>
            );
        }

    }

    if (typeof value === 'string') {
        const option = PredefinedExpressions.getExpressionOption(value as string, playbook, step);
        const context = option?.context
            ? <span className="text-gray-500 ml-1">{option.context}</span>
            : null;

        if (option) {
            return (
                <em
                    data-tooltip-id="global-tooltip"
                    data-tooltip-content={option.value}
                    className="has-tooltip-arrow">{option.label}{context}</em>
            );
        }
    }
    else if (Array.isArray(value)) {
        return (<span>
            {(value as Option[]).map((option, index) => formatArrayValue(option, index))}
        </span>);
    }
    else if (typeof value === 'object') {
        // This must be a single option.
        const option = value as Option;
        return <span className="whitespace-pre-line has-tooltip-arrow"
                     data-tooltip-id="global-tooltip"
                     data-tooltip-content={option.value}>
            {option.label}
        </span>;
    }

    return (
        <span className="whitespace-pre-line">{value?.toString()}</span>
    );
}