import AwesomeDebouncePromise from "awesome-debounce-promise";
import AsyncSelect from "react-select/async";
import {Option} from "../../models/options";

export function SegmentsDropDown({onChange, value, required, placeholder} : {onChange: (option: unknown) => void, value: Option[], required?: boolean, placeholder?: string}) {
    async function fetchOptions(inputValue): Promise<Option[]> {
        const params = new URLSearchParams({
            q: inputValue,
            limit: '-1', // No limit!
        });
        const response = await fetch(`/api/internal/customers/segments/typeahead?${params.toString()}`);

        return await response.json();
    }

    const loadOptions = AwesomeDebouncePromise(fetchOptions, 250);

    return (
        <AsyncSelect defaultOptions={true}
                     isMulti={true}
                     placeholder={placeholder}
                     value={value}
                     required={!!required}
                     onChange={onChange}
                     loadOptions={loadOptions} />
    );
}