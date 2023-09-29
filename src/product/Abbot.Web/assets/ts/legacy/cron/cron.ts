import cronstrue from 'cronstrue';
/**
 * Used to represent a cron value.
 */
export const ScheduleTypes = ['never' , 'minutes' , 'hourly' , 'daily' , 'weekly' , 'monthly' , 'cron'];
export type ScheduleType = typeof ScheduleTypes[number];

/* Parses the value as a natural number or returns undefined */
export function parseNaturalNumber(value: string) : number | undefined {
    if (/^\d+$/.test(value)) {
        return parseInt(value);
    }
    return undefined;
}

export class Cron {
    minute: string;
    hour: string;
    day: string;
    month: string;
    weekdays: string[];

    constructor(schedule: string);
    constructor(minute: string, hour: string, day: string, month: string, weekdays: string);
    constructor(...schedule: string[]) {
        const parts = schedule.length === 5
            ? schedule
            : schedule[0].split(' ');
        if (parts.length !== 5) {
            throw 'A cron schedule may only have 5 parts.';
        }
        
        this.minute = parts[0];
        this.hour = parts[1];
        this.day = parts[2];
        this.month = parts[3];
        this.weekdays = parts[4].split(',')
    }

    static fromType(type: ScheduleType) {
        switch (type) {
            case 'never': return Cron.never();
            case 'minutes': return Cron.everyTenMinutes();
            case 'hourly': return Cron.hourly();
            case 'daily': return Cron.daily();
            case 'weekly': return Cron.weekly();
            case 'monthly': return Cron.monthly();
            case 'cron': return null;
        }
    }
    
    static never() {
        return new Cron("0 0 31 2 *");
    }

    static everyTenMinutes() {
        return new Cron("*/10 * * * *");
    }

    static hourly() {
        return new Cron("0 * * * *");
    }

    static daily() {
        return new Cron("0 0 * * *");
    }

    static weekly() {
        return new Cron("0 0 * * 0");
    }

    static monthly() {
        return new Cron("0 0 1 * *");
    }
    /*
     * @param {Cron} cron - a cron schedule to combine. 
     */
    public combine(cron: Cron) {
        const specific = (a: string, b: string) => {
            if (a === "*") return b;
            if (b === "*") return a;
            return a > b ? a : b;
        };
        
        // Combine the two arrays, get the union, sort them.
        const incomingDays = [...cron.weekdays].filter(d => d !== '*');
        const ourDays = [...this.weekdays].filter(d => d !== '*');
        const days = [...new Set([...incomingDays, ...ourDays])]
            .sort();
        
        return new Cron(
            specific(this.minute, cron.minute),
            specific(this.hour, cron.hour),
            specific(this.day, cron.day),
            specific(this.month, cron.month),
            days.length === 0 ? '*' : days.join(',')
        );
    }

    public addDayOfWeek(cron: string) {
        const addCron = new Cron(cron);
        return this.combine(addCron);
    }

    public removeDayOfWeek(cron: string) {
        const removeCron = new Cron(cron);
        const days = this.weekdays.filter(d => !removeCron.weekdays.includes(d));
        return this.withDaysOfWeek(days);
    }

    public withDaysOfWeek(weekdays: string[]) {
        return new Cron(this.minute, this.hour, this.day, this.month, weekdays.join(','));
    }

    /*
    * Returns a new Cron with the specified minute.
    * */
    get minutes() {
        return new Cron(this.minute, "*", "*", "*", "*");
    }

    /*
     Returns a new Cron with just the time portion of this cron.
    */
    get time() {
        return new Cron(this.minute, this.hour, "*", "*", "*");
    }

    // Returns just the month day portion of the cron.
    get monthDay() {
        return new Cron("0", "0", this.day, "*", "*");
    }
    
    get type(): ScheduleType {
        if (this.month === '2' && (this.day === '30' || this.day === '31')) {
            return 'never';
        }
        
        if (this.anyHour && this.anyDay && this.anyMonth && this.anyWeekday) {
            if (this.minute === '*/10') {
                return "minutes";
            }
            const minute = parseNaturalNumber(this.minute);
            if (minute !== undefined && minute >= 0 && minute <= 50 && minute % 10 === 0) {
                return "hourly";
            }
        }
        
        if (this.anyDay && this.anyMonth) {
            if (this.minute === '0' || this.minute === '30') {
                const hour = parseNaturalNumber(this.hour);
                if (hour !== undefined && hour >= 0 && hour < 24) {
                    if (this.anyWeekday) {
                        return "daily";
                    }
                    if (this.weekdays.map(w => parseInt(w)).every(w => w >= 0 && w < 7)) {
                        return "weekly";
                    }
                }
            }
        }
        
        if (this.anyMonth && this.anyWeekday) {
            if (this.minute === '0' || this.minute === '30') {
                const hour = parseNaturalNumber(this.hour);
                if (hour !== undefined && hour >= 0 && hour < 24) {
                    return "monthly";
                }
            }
        }
        
        return "cron";
    }
    
    get schedule() {
        return `${this.minute} ${this.hour} ${this.day} ${this.month} ${this.weekdays.join(',')}`
    }
    
    get anyMinute() {
        return this.minute === '*';
    }

    get anyHour() {
        return this.hour === '*';
    }

    get anyDay() {
        return this.day === '*';
    }

    get anyMonth() {
        return this.month === '*';
    }

    get anyWeekday() {
        return this.weekdays.length === 1 && this.weekdays[0] === '*';
    }

    /**
     * Provides a human-friendly description of the cron schedule.
     * If a description can't be determined, returns the cron schedule.
     */
    public get description(): string {
        if (this.type === "never") {
            return "Never";
        }

        return cronstrue.toString(this.schedule);
    }
}