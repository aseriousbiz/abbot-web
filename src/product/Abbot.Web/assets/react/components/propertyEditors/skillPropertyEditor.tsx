import {StepPropertyProps} from "./propertyEditor";
import { AsyncSelectPropertyEditor } from "./asyncSelectPropertyEditor";

export function SkillPropertyEditor(props: StepPropertyProps<string>) {
    return <AsyncSelectPropertyEditor
        {...props}
        placeholder="Search for a skill…"
        fetchUrl="/api/internal/skills/typeahead" />
}
