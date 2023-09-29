import { PropertyViewerProps } from ".";
import { DurationValue } from "../../models/durationValue";

export default function DurationPropertyViewer({ value }: PropertyViewerProps) {
    if (typeof value !== 'string') {
        throw new Error("Expected duration to be a string");
    }
    return <>{`${DurationValue.parse(value)}`}</>;
}