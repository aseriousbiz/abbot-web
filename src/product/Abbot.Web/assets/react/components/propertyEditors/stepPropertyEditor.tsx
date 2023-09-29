import {PropsWithKey} from "../../models/propsWithKey";
import {
    BooleanPropertyEditor,
    FloatPropertyEditor,
    IntegerPropertyEditor,
    PropertyEditor,
    PropertyLabel,
    StepPropertyProps,
    StringPropertyEditor
} from "./propertyEditor";
import DurationPropertyEditor from "./durationPropertyEditor";
import {ComparisonTypePropertyEditor} from "./comparisonTypePropertyEditor";
import {OptionsPropertyEditor} from "./optionsPropertyEditor";
import {PredefinedExpressionPropertyEditor} from "./predefinedExpressionPropertyEditor";
import {SignalPropertyEditor} from "./signalPropertyEditor";
import {SkillPropertyEditor} from "./skillPropertyEditor";
import {TimezonePropertyEditor} from "./timezonePropertyEditor";
import {SchedulePropertyEditor} from "./schedulePropertyEditor";
import {NotificationTypePropertyEditor} from "./notificationTypePropertyEditor";
import {CustomerPropertyEditor} from "./customerPropertyEditor";
import {RichTextPropertyEditor} from "./richTextPropertyEditor";
import {isStaffMode} from "../../../ts/env";
import {PropertyTypeKind} from "../../models/step";
import {ComparandPropertyEditor} from "./comparandPropertyEditor";
import { ChannelPropertyEditor, EmojiPropertyEditor, MemberPropertyEditor, MessageTargetPropertyEditor } from "./asyncSelectPropertyEditor";
import {CustomerSegmentsPropertyEditor} from "./customerSegmentsPropertyEditor";

const propertyEditorLookup: Partial<Record<PropertyTypeKind, (props: StepPropertyProps<unknown>) => JSX.Element>> = {
    'Boolean': BooleanPropertyEditor,
    'Channel': ChannelPropertyEditor,
    'Emoji': EmojiPropertyEditor,
    'Member': MemberPropertyEditor,
    'MessageTarget': MessageTargetPropertyEditor,
    'ComparisonType': ComparisonTypePropertyEditor,
    'Comparand': ComparandPropertyEditor,
    'Customer': CustomerPropertyEditor,
    'CustomerSegments': CustomerSegmentsPropertyEditor,
    'Duration': DurationPropertyEditor,
    'Float': FloatPropertyEditor,
    'Integer': IntegerPropertyEditor,
    'NotificationType': NotificationTypePropertyEditor,
    'Options': OptionsPropertyEditor,
    'PredefinedExpression': PredefinedExpressionPropertyEditor,
    'Schedule': SchedulePropertyEditor,
    'Signal': SignalPropertyEditor,
    'Skill': SkillPropertyEditor,
    'String': StringPropertyEditor,
    'Timezone': TimezonePropertyEditor,
    'RichText': RichTextPropertyEditor,
};

export default function StepPropertyEditor({ step, property, value, onChange, inputs }: PropsWithKey<StepPropertyProps<unknown>>) {
    if (property.hidden && !isStaffMode) {
        return null;
    }

    const Editor = propertyEditorLookup[property.type.kind] || EditorNotFound;
    return (<Editor step={step} property={property} value={value} onChange={onChange} inputs={inputs} />);
}

function EditorNotFound({ property }: StepPropertyProps<unknown>) {
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <div>
                    <i className="fas fa-exclamation-triangle text-yellow-500 mr-2"></i>
                    No editor for properties of type <code>{property.type.kind}</code>.
                </div>
            </PropertyLabel>
        </PropertyEditor>
    );
}
