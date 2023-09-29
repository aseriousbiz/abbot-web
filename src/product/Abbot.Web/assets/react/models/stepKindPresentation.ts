import { StepKind } from "./step";

export type Colors = {
    iconBgColor: string;
    iconTextColor: string;
    textColor: string;
    border: string;
    darkBorder: string;
};

export default class StepKindPresentation {
    colors: Colors;

    static colorsLookup = {
        ['Trigger']: {
            iconBgColor: 'bg-amber-200',
            iconTextColor: 'text-amber-800',
            textColor: "text-amber-500",
            border: 'border-amber-300',
            darkBorder: 'border-amber-500',
        },
        ['Action']: {
            iconBgColor: 'bg-indigo-200',
            iconTextColor: 'text-indigo-800',
            textColor: "text-indigo-500",
            border: 'border-indigo-300',
            darkBorder: 'border-indigo-500',
        },
    };

    public static getPresentation(stepKind: StepKind): StepKindPresentation {
        return new StepKindPresentation(stepKind);
    }

    constructor(stepKind: StepKind) {
        this.colors = StepKindPresentation.colorsLookup[stepKind];
    }
}