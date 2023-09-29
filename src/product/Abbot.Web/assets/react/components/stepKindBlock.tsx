import {StepType} from "../../ts/api/internal";
import StepKindPresentation from "../models/stepKindPresentation";
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import {IconProp} from "@fortawesome/fontawesome-svg-core";
import Debug from "./debug";

export default function StepKindBlock(props: { stepType: StepType }) {
    const stepType = props.stepType;
    const colors = StepKindPresentation.getPresentation(stepType.kind).colors;

    const iconName = stepType.presentation.icon;
    // If the icon is not available, make sure to import it in the index.tsx file.
    const icon = `fa-regular ${iconName}` as IconProp;

    return (
        <div className="flex gap-x-2 items-center">
            <div className={`rounded-md ${colors.iconBgColor} p-2 flex justify-center items-center w-8 h-8`}>
                <FontAwesomeIcon icon={icon} className={colors.iconTextColor} />
            </div>

            <div>
                <p className={`font-medium text-xs capitalize ${colors.iconTextColor} text-left`}>
                    {stepType.kind}
                </p>

                <p className="font-medium text-sm">
                    {stepType.presentation.label}
                </p>
                <Debug>
                    <code className="text-xs">{stepType.name}</code>
                </Debug>
            </div>
        </div>
    );
}
