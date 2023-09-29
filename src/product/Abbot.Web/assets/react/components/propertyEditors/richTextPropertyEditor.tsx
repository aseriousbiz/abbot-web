import {convertToTipTapDocument} from "../valueConverters";
import RichTextInput from "../mrkdwn/richTextInput";
import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {JSONContent} from "@tiptap/react";
import usePlaybook from "../../hooks/usePlaybooks";
import { useMemo } from "react";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import { createTipTapExtensions } from "../mrkdwn/extensions";

export function RichTextPropertyEditor({ step, property, value, onChange }: StepPropertyProps<JSONContent>) {
    const { playbook } = usePlaybook();

    const extensions = useMemo(
        () => {
            const expressions = PredefinedExpressions.getAvailableExpressionOptions(
                playbook,
                step,
                true);
            return createTipTapExtensions(expressions)
        },
        [playbook, step]);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <RichTextInput
                    defaultValue={convertToTipTapDocument(value)}
                    onChange={onChange}
                    required={!!property.required}
                    rows={property.type.editRows}
                    extensions={extensions} />
            </PropertyLabel>
        </PropertyEditor>
    )
}
