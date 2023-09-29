import {MultiSelectProps} from "../../models/options";
import AwesomeDebouncePromise from "awesome-debounce-promise";
import AsyncSelect from "react-select/async";

export interface FetchMultiSelectProps extends MultiSelectProps {
    type: string;
}

export function FetchMultiSelect({property, value, onChange, type}: FetchMultiSelectProps) {
    async function fetchOptions(inputValue: string) {
        const params = new URLSearchParams({
            q: inputValue,
            limit: '-1', // No limit!
        });
        const response = await fetch(`/api/internal/${type}/typeahead?${params.toString()}`);

        return await response.json();
    }

    const loadOptions = AwesomeDebouncePromise(fetchOptions, 250);

    return (
        <AsyncSelect defaultOptions={true}
                     value={value}
                     isMulti={true}
                     required={!!property.required}
                     onChange={onChange}
                     loadOptions={loadOptions} />
    );
}

export function ChannelMultiSelect({property, value, onChange}: MultiSelectProps) {
    return (
        <FetchMultiSelect type="rooms"
                          property={property}
                          value={value}
                          onChange={onChange} />
    );
}

export function CustomerMultiSelect({property, value, onChange}: MultiSelectProps) {
    return (
        <FetchMultiSelect type="customers"
                          property={property}
                          value={value}
                          onChange={onChange} />
    );
}