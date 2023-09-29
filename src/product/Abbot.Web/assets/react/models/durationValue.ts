import moment from "moment";

export enum DurationUnits {
    Minutes = 'minutes',
    Hours = 'hours',
    Days = 'days',
    Months = 'months',
    Years = 'years',
}

export class DurationValue {
    private duration: moment.Duration;
    magnitude: number;
    unit: DurationUnits;

    constructor(duration: moment.Duration) {
        this.duration = duration;
        [this.magnitude, this.unit] = DurationValue.durationToMagnitudeAndUnit(this.duration);
    }

    /** Returns a new DurationValue with the specified magnitude. */
    withMagnitude(magnitude: number): DurationValue {
        return new DurationValue(moment.duration(magnitude, this.unit));
    }

    /** Returns a new DurationValue with the specified unit. */
    withUnit(unit: DurationUnits): DurationValue {
        return new DurationValue(moment.duration(this.magnitude, unit));
    }

    toISOString(): string {
        return this.duration.toISOString();
    }

    toString(): string {
        return `${this.magnitude} ${this.unit}`;
    }

    static parse(value: string): DurationValue {
        return new DurationValue(moment.duration(value));
    }

    // Converts a moment duration into a magnitude and a unit.
    // The unit is one of the DurationUnits enum values, and represents the largest unit that can represent the duration as a whole number.
    // The magnitude is the number of units.
    static durationToMagnitudeAndUnit(duration?: moment.Duration): [number, DurationUnits] {
        if (!duration) {
            return [0, DurationUnits.Minutes];
        }

        const seconds = duration.asSeconds();
        const minutes = duration.asMinutes();
        const hours = duration.asHours();
        const days = duration.asDays();
        const months = duration.asMonths();
        const years = duration.asYears();

        if (years % 1 === 0) {
            return [years, DurationUnits.Years];
        }

        if (months % 1 === 0) {
            return [months, DurationUnits.Months];
        }

        if (days % 1 === 0) {
            return [days, DurationUnits.Days];
        }

        if (hours % 1 === 0) {
            return [hours, DurationUnits.Hours];
        }

        if (minutes % 1 === 0) {
            return [minutes, DurationUnits.Minutes];
        }

        // If this ain't whole, the user's doing something weird 🤷.
        return [seconds * 60, DurationUnits.Minutes];
    }
}