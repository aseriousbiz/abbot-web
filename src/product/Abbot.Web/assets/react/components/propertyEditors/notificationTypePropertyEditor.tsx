import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {DropDown} from "./formControls";

const NotificationTypes = ['Warning', 'Deadline'];
export type NotificationType = typeof NotificationTypes[number];

export function NotificationTypePropertyEditor({ property, value, onChange }: StepPropertyProps<string>) {
    const options = NotificationTypes.map((comparisonType) => (
        <option value={comparisonType} key={comparisonType}>{comparisonType}</option>
    ));
    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <DropDown name={property.name}
                    onChange={onChange}
                    required={!!property.required}
                    value={value}>
                    {!value && <option key=''></option> /* force picking a value if no default */}
                    {options}
                </DropDown>
            </PropertyLabel>
        </PropertyEditor>
    );
}