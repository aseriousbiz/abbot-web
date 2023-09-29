import { isStaffMode } from "../../ts/env"

type FeatureSparkleProps = {
    flags: string | string[],
}

export default function FeatureSparkle({ flags }: FeatureSparkleProps) {
    const flagNames = Array.isArray(flags) ? flags.join(', ') : flags;
    const flagsMessage = Array.isArray(flags) && flags.length > 1 ? `${flagNames} feature flags` : `${flagNames} feature flag`;
    return (
        <span className="ml-1"
              data-tooltip-id="step-block-tooltip"
              data-tooltip-content={`${isStaffMode ? `Preview Feature, requires the ${flagsMessage}` : "Preview Feature"}`}>
            <i className="fa-duotone fa-sparkles"></i>
        </span>
    )
}