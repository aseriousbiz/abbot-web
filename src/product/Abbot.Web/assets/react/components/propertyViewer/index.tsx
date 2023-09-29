import {ChannelPropertyViewer, MemberPropertyViewer, MessageTargetPropertyViewer} from "./asyncSelectPropertyViewer";
import DurationPropertyViewer from "./durationPropertyViewer";
import OptionsPropertyViewer from "./optionsPropertyViewer";
import SchedulePropertyViewer from "./schedulePropertyViewer";
import RichTextPropertyViewer from "./richTextPropertyViewer";
import {PlacedStep, PropertyTypeKind, StepProperty} from "../../models/step";
import ComparandPropertyViewer from "./comparandPropertyViewer";
import DefaultValueViewer from "./defaultValueViewer";
import {ComparisonTypePropertyViewer} from "./comparisonTypePropertyViewer";
import {CustomerSegmentsPropertyViewer} from "./customerSegmentsPropertyViewer";

export interface PropertyViewerProps<T = unknown> {
    property: StepProperty,
    value: T,
    inputs: Record<string, unknown>,
    step?: PlacedStep,
}

// Record<> enforces a property for each enum value (good for editors!); Partial<Record<>> allows a subset
const propertyViewerLookup: Partial<Record<PropertyTypeKind, (props: PropertyViewerProps) => JSX.Element>> = {
    'Channel': ChannelPropertyViewer,
    'MessageTarget': MessageTargetPropertyViewer,
    'Member': MemberPropertyViewer,
    'Duration': DurationPropertyViewer,
    'Options': OptionsPropertyViewer,
    'Schedule': SchedulePropertyViewer,
    'RichText': RichTextPropertyViewer,
    'Comparand': ComparandPropertyViewer,
    'ComparisonType': ComparisonTypePropertyViewer,
    'CustomerSegments': CustomerSegmentsPropertyViewer,
};

export function PropertyViewer(props: PropertyViewerProps) {
    const Viewer = propertyViewerLookup[props.property.type.kind] || DefaultPropertyViewer;
    return <Viewer {...props} />;
}

function DefaultPropertyViewer(props: PropertyViewerProps) {
    return (
        <DefaultValueViewer {...props} />
    );
}
