import {PropertyViewerProps} from ".";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import {createTipTapExtensions} from "../mrkdwn/extensions";
import RichTextInput from "../mrkdwn/richTextInput";
import {convertToTipTapDocument} from "../valueConverters";
import usePlaybook from "../../hooks/usePlaybooks";
import {useMemo} from "react";

export default function RichTextPropertyViewer({ value, step }: PropertyViewerProps) {
    const { playbook } = usePlaybook();
    if (typeof value !== 'string' && typeof value !== 'object') {
        throw new Error("Expected message to be a string or object");
    }

    const extensions = useMemo(
        () => {
            // Just grab all of them because we're rendering the viewer.
            const expressions = PredefinedExpressions.all;
            const options = PredefinedExpressions.mapExpressionsToOptions(
                expressions,
                playbook,
                step,
                true);

            return createTipTapExtensions(options);
        }
        , [playbook]);

    return (
        <RichTextInput value={convertToTipTapDocument(value)} readOnly={true} extensions={extensions} />
    );
}