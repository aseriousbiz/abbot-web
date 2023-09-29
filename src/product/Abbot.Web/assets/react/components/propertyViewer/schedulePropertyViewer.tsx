import {PropertyViewerProps} from ".";
import { Schedule, SerializedSchedule } from "../../models/schedule";

export default function SchedulePropertyViewer({ value }: PropertyViewerProps) {
    if (typeof value !== 'string' && typeof value !== 'object') {
        throw new Error("Expected schedule to be a string or object");
    }
    const schedule = Schedule.parse(value as string | SerializedSchedule);

    return <span>{schedule.description}</span>;
}