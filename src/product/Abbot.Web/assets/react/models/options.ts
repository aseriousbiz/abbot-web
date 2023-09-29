import { UIHint } from "../../ts/api/internal";
import {StepProperty} from "./step";

export interface Option {
    label: string,
    value: string,
}

export interface ExpressionOption extends ExtendedOption {
    expression: string,
}

export interface ExtendedOption extends Option {
    context?: string,
    isExpression?: boolean,
}

export interface OptionsDefinition {
    preset?: string;
    name: string;
    options: Option[];
}

const satisfied5: Option[] = [
    { value: '5', label: 'Very satisfied' },
    { value: '4', label: 'Satisfied' },
    { value: '3', label: 'Neutral' },
    { value: '2', label: 'Unsatisfied' },
    { value: '1', label: 'Very unsatisfied' },
];

const likely5: Option[] = [
    { value: '5', label: 'Very likely' },
    { value: '4', label: 'Likely' },
    { value: '3', label: 'Neutral' },
    { value: '2', label: 'Unlikely' },
    { value: '1', label: 'Very unlikely' },
];

const yesNo: Option[] = [
    { value: 'yes', label: 'Yes' },
    { value: 'no', label: 'No' },
]

export type OptionsKind = Extract<UIHint, 'Poll'>;

const presets: Record<OptionsKind, OptionsDefinition[]> = {
    'Poll': [
        { preset: 'likert-5-satisfied', name: 'Customer Satisfaction', options: satisfied5 },
        { preset: 'likert-5-likely', name: 'Likelihood', options: likely5 },
        { preset: 'yes-no', name: 'Yes or No', options: yesNo },
    ],
};

export function findPresets(kind: string | undefined): OptionsDefinition[] {
    if (kind in presets) {
        return presets[kind];
    }

    throw new Error(`Unexpected options kind '${kind}'`);
}

export interface MultiSelectProps {
    property: StepProperty,
    value: Option[],
    onChange: (value: unknown) => void,
}
