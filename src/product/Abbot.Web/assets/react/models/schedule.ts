export enum ScheduleType {
    Hourly = 'hourly',
    Daily = 'daily',
    Weekly = 'weekly',
    Monthly = 'monthly',
    Advanced = 'advanced',
}

export type DayOfMonth = number;
export const allDaysOfMonth: DayOfMonth[] = Array.from(Array(31).keys())
    .map(i => i + 1 as DayOfMonth)

export enum Weekday {
    Sunday = 'sunday',
    Monday = 'monday',
    Tuesday = 'tuesday',
    Wednesday = 'wednesday',
    Thursday = 'thursday',
    Friday = 'friday',
    Saturday = 'saturday',
}

export type SerializedSchedule =
    { type: ScheduleType.Hourly, minute: number } |
    { type: ScheduleType.Daily, hour: number, minute: number } |
    { type: ScheduleType.Weekly, hour: number, minute: number, weekdays: Weekday[] } |
    { type: ScheduleType.Monthly, hour: number, minute: number, dayOfMonth: DayOfMonth } |
    { type: ScheduleType.Advanced, cron: string };

export function describeDayOfMonth(value: DayOfMonth) {
    switch (value) {
        case 1: return '1st';
        case 2: return '2nd';
        case 3: return '3rd';
        case 21: return '21st';
        case 22: return '22nd';
        case 23: return '23rd';
        case 31: return '31st';
        default: return `${value}th`;
    }
}

export abstract class Schedule {
    constructor(public type: ScheduleType) {
    }

    static parse(input: SerializedSchedule | string) {
        let serialized: SerializedSchedule;
        if (!input) {
            // Default to daily at noon
            return new DailySchedule(12, 0);
        } else if (typeof input === 'string') {
            // We have to hack things a bit.
            // Any old schedules will be a cron string, so we check to see if this is valid JSON
            try {
                serialized = JSON.parse(input) as SerializedSchedule;
            } catch {
                // Invalid JSON, just use the value as a cron string
                return new AdvancedSchedule(input);
            }
        } else if (typeof input === 'object' && 'type' in input) {
            serialized = input as SerializedSchedule;
        } else {
            throw new Error('Invalid schedule');
        }

        switch (serialized.type) {
            case ScheduleType.Hourly:
                return new HourlySchedule(serialized.minute || 0);
            case ScheduleType.Daily:
                return new DailySchedule(serialized.hour || 0, serialized.minute || 0);
            case ScheduleType.Weekly:
                return new WeeklySchedule(serialized.hour || 0, serialized.minute || 0, serialized.weekdays || []);
            case ScheduleType.Monthly:
                return new MonthlySchedule(serialized.hour || 0, serialized.minute || 0, serialized.dayOfMonth || 1);
            case ScheduleType.Advanced:
                return new AdvancedSchedule(serialized.cron || '10 * * * *');
        }
    }

    static create(type: ScheduleType) {
        switch (type) {
            case ScheduleType.Hourly:
                return new HourlySchedule(0);
            case ScheduleType.Daily:
                return new DailySchedule(0, 0);
            case ScheduleType.Weekly:
                return new WeeklySchedule(0, 0, []);
            case ScheduleType.Monthly:
                return new MonthlySchedule(0, 0, 1);
            case ScheduleType.Advanced:
                return new AdvancedSchedule('10 * * * *');
        }
    }

    abstract get description(): string;
    abstract serialize(): SerializedSchedule;
}

export class HourlySchedule extends Schedule {
    constructor(public minute: number) {
        super(ScheduleType.Hourly);
    }

    serialize(): SerializedSchedule {
        return { type: ScheduleType.Hourly, minute: this.minute };
    }

    get description(): string {
        const mins = this.minute.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        return `Every hour at :${mins}`;
    }
}

export class DailySchedule extends Schedule {
    constructor(public hour: number, public minute: number) {
        super(ScheduleType.Daily);
    }

    serialize(): SerializedSchedule {
        return { type: ScheduleType.Daily, hour: this.hour, minute: this.minute };
    }

    get description(): string {
        const hr = this.hour.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        const mins = this.minute.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        return `Every day at ${hr}:${mins}`;
    }
}

export class WeeklySchedule extends Schedule {
    constructor(public hour: number, public minute: number, public weekdays: Weekday[]) {
        super(ScheduleType.Weekly);
    }

    serialize(): SerializedSchedule {
        return { type: ScheduleType.Weekly, hour: this.hour, minute: this.minute, weekdays: this.weekdays };
    }

    get description(): string {
        const weekdays = this.weekdays.map(w => w[0].toUpperCase() + w.slice(1)).join(', ');
        const hr = this.hour.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        const mins = this.minute.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        return `Every ${weekdays} at ${hr}:${mins}`;
    }
}

export class MonthlySchedule extends Schedule {
    constructor(public hour: number, public minute: number, public dayOfMonth: DayOfMonth) {
        super(ScheduleType.Monthly);
    }

    serialize(): SerializedSchedule {
        return { type: ScheduleType.Monthly, hour: this.hour, minute: this.minute, dayOfMonth: this.dayOfMonth };
    }

    get description(): string {
        const hr = this.hour.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        const mins = this.minute.toLocaleString(undefined, { minimumIntegerDigits: 2 });
        return `Every month on the ${describeDayOfMonth(this.dayOfMonth)} at ${hr}:${mins}`;
    }
}

export class AdvancedSchedule extends Schedule {
    constructor(public cron: string) {
        super(ScheduleType.Advanced);
    }

    serialize(): SerializedSchedule {
        return { type: ScheduleType.Advanced, cron: this.cron };
    }

    get description(): string {
        return `Advanced: ${this.cron}`;
    }
}