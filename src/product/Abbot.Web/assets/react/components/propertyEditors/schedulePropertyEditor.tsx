import { useCallback, useState } from "react";
import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DropDown, TextInput} from "./formControls";
import { AdvancedSchedule, DailySchedule, DayOfMonth, HourlySchedule, MonthlySchedule, Schedule, ScheduleType, SerializedSchedule, Weekday, WeeklySchedule, allDaysOfMonth, describeDayOfMonth } from "../../models/schedule";

type ScheduleEditorProps<T extends Schedule> = {
    value: T,
    onChange: (value: T) => void,
}

const scheduleTypeLabels: Record<ScheduleType, string> = {
    [ScheduleType.Hourly]: "Every hour…",
    [ScheduleType.Daily]: "Every day…",
    [ScheduleType.Weekly]: "Every week…",
    [ScheduleType.Monthly]: "Every month on the…",
    [ScheduleType.Advanced]: "Advanced…",
};

export function SchedulePropertyEditor({ property, value, onChange }: StepPropertyProps<SerializedSchedule>) {
    const [schedule, setSchedule] = useState<Schedule>(() => Schedule.parse(value));

    const optionElements = Object.entries(scheduleTypeLabels).map(([key, label]) => {
        return <option key={key} value={key}>{label}</option>
    });

    function scheduleTypeChanged(selected: string) {
        const type = selected as ScheduleType;

        // For now, at least, changing the schedule type resets the schedule
        const schedule = Schedule.create(type);

        handleChange(schedule);
    }

    const handleChange = useCallback((s: Schedule) => {
        setSchedule(s);
        onChange(s.serialize());
    }, [onChange]);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <DropDown value={schedule.type} onChange={scheduleTypeChanged}>
                    {optionElements}
                </DropDown>
            </PropertyLabel>
            {schedule instanceof HourlySchedule
                ? <HourlyEditor value={schedule} onChange={handleChange} />
                : null}
            {schedule instanceof DailySchedule
                ? <DailyEditor value={schedule} onChange={handleChange} />
                : null}
            {schedule instanceof WeeklySchedule
                ? <WeeklyEditor value={schedule} onChange={handleChange} />
                : null}
            {schedule instanceof MonthlySchedule
                ? <MonthlyEditor value={schedule} onChange={handleChange} />
                : null}
            {schedule instanceof AdvancedSchedule
                ? <AdvancedEditor value={schedule} onChange={handleChange} required={!!property.required} />
                : null}
        </PropertyEditor>
    );
}

function HourlyEditor({ value, onChange }: ScheduleEditorProps<HourlySchedule>) {
    return <div className="flex items-baseline gap-2">
        <span className="font-medium text-gray-500 text-sm">at</span>
        <div className="flex items-baseline gap-x-1">
            <span>HH:</span>
            <MinuteDropDown value={value.minute} onChange={min => onChange(new HourlySchedule(min))} />
        </div>
    </div>
}

function DailyEditor({value, onChange} : ScheduleEditorProps<DailySchedule>) {
    return <div className="flex gap-2 items-baseline">
        <span className="font-medium text-gray-500 text-sm">at</span>
        <div className="flex items-baseline gap-x-1">
            <HourDropDown value={value.hour} onChange={hr => onChange(new DailySchedule(hr, value.minute))} />
            <span>:</span>
            <MinuteDropDown value={value.minute} onChange={min => onChange(new DailySchedule(value.hour, min))} />
        </div>
    </div>
}

const shortWeekdays: { [key in Weekday]: string } = {
    [Weekday.Sunday]: "Su",
    [Weekday.Monday]: "M",
    [Weekday.Tuesday]: "Tu",
    [Weekday.Wednesday]: "W",
    [Weekday.Thursday]: "Th",
    [Weekday.Friday]: "F",
    [Weekday.Saturday]: "Sa",
};

function WeeklyEditor({value, onChange} : ScheduleEditorProps<WeeklySchedule>) {
    function setDay(day: Weekday, active: boolean) {
        let newDays = value.weekdays;
        if (active && !newDays.includes(day)) {
            newDays.push(day);
        } else if (!active && newDays.includes(day)) {
            newDays = newDays.filter(d => d !== day);
        }
        onChange(new WeeklySchedule(value.hour, value.minute, newDays));
    }

    const days = Object.values(Weekday).map(d => {
        return <label key={d} className="btn text-slate-700 grow flex flex-col p-1 gap-1 text-xs items-center">
            <input type="checkbox" checked={value.weekdays.includes(d)} onChange={e => setDay(d, e.target.checked)} />
            {shortWeekdays[d]}
        </label> 
    });

    return <>
        <div className="mt-1 w-full flex flex-col">
            <p className="font-medium text-sm text-gray-500">on days</p>
            <div className="mt-1 w-full flex gap-x-1 justify-items-stretch">
                {days}
            </div>
        </div>
        <DailyEditor value={value} onChange={daily => onChange(new WeeklySchedule(daily.hour, daily.minute, value.weekdays))} />
    </>
}

function MonthlyEditor({value, onChange} : ScheduleEditorProps<MonthlySchedule>) {
    const daysOfMonth = allDaysOfMonth.map((d) => {
        return (
            <option key={d} value={d}>{describeDayOfMonth(d)}</option>
        );
    });

    function setDayOfMonth(day: string) {
        const dayOfMonth: DayOfMonth = parseInt(day);
        onChange(new MonthlySchedule(value.hour, value.minute, dayOfMonth));
    }

    return <>
        <div className="mt-1 w-full gap-2 flex justify-items-stretch items-baseline">
            <span className="font-medium text-sm text-slate-500">on the</span>
            <DropDown value={value.dayOfMonth} onChange={v => setDayOfMonth(v)}>
                {daysOfMonth}
            </DropDown>
        </div>
        <DailyEditor value={value} onChange={daily => onChange(new MonthlySchedule(daily.hour, daily.minute, value.dayOfMonth))} />
    </>
}

type AdvancedEditorProps = ScheduleEditorProps<AdvancedSchedule> & {
    required: boolean
};

function AdvancedEditor({value, onChange, required} : AdvancedEditorProps) {
    return <div className="flex flex-col gap-1">
        <span className="text-sm font-medium text-slate-500">Enter a cron expression</span>
        <TextInput value={value.cron} onChange={v => onChange(new AdvancedSchedule(v))} required={required} />
    </div>
}

function MinuteDropDown({ value, onChange }: { value: number, onChange: (value: number) => void }) {
    const options = Array.from(Array(12).keys()).map(i => {
        const min = i * 5;
        return (
            <option key={i} value={min}>{min.toLocaleString(undefined, { minimumIntegerDigits: 2 })}</option>
        );
    });
    return <DropDown value={value} onChange={v => onChange(parseInt(v))}>
        {options}
    </DropDown>
}

function HourDropDown({ value, onChange }: { value: number, onChange: (value: number) => void }) {
    const options = Array.from(Array(24).keys()).map(i => {
        return (
            <option key={i} value={i}>{i.toLocaleString(undefined, { minimumIntegerDigits: 2 })}</option>
        );
    });
    return <DropDown value={value} onChange={v => onChange(parseInt(v))}>
        {options}
    </DropDown>
}
