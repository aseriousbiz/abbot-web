import {PropertyEditor, PropertyLabel, StepPropertyProps} from "./propertyEditor";
import {HandlebarExpression, PredefinedExpressions} from "../../models/predefinedExpressions";
import { defaultProps } from 'react-select/base';
import AsyncSelect from 'react-select/async';
import {useMemo, useState} from "react";
import AwesomeDebouncePromise from 'awesome-debounce-promise';
import usePlaybook from "../../hooks/usePlaybooks";
import {PropsValue, SingleValue} from "react-select";
import Icon from "../icon";
import { appInsights } from "../../../ts/telemetry";
import logger from "../../../ts/log";
import {ExtendedOption} from "../../models/options";

const log = logger("asyncSelectPropertyEditor");

(defaultProps as unknown as { classNamePrefix: string }).classNamePrefix = 'fixed-select';

export interface AsyncSelectOption extends ExtendedOption {
    image?: string,
    isLoading?: boolean,
    isError?: boolean,
}

type AsyncSelectPropertyEditorProps = StepPropertyProps<string | string[]> & {
    expressions?: HandlebarExpression[];
    fetchUrl: string;
    placeholder?: string;
};

function isSingleValue<T>(inp: PropsValue<T>): inp is SingleValue<T> {
    return !Array.isArray(inp);
}

export function AsyncSelectPropertyEditor({ step, property, value, expressions, fetchUrl, onChange, placeholder }: AsyncSelectPropertyEditorProps) {
    const { playbook } = usePlaybook();

    const expressionOptions = useMemo<AsyncSelectOption[]>(
        () => {
            return expressions
                ? PredefinedExpressions.mapExpressionsToOptions(expressions, playbook, step, false)
                : [];
        },
        [playbook, step]
    );

    // Normalize incoming value to an array
    const normalizedValues = Array.isArray(value)
        ? value
        : value
            ? [value]
            : [];

    // Convert the normalized values to synthetic options to use until we've done the first fetch.
    // Set the current options to the synthetic options.
    const [currentOptions, setCurrentOptions] = useState<AsyncSelectOption[]>(normalizedValues.map(v => {
        return PredefinedExpressions.getExpressionOption(v, playbook, step)
            ?? { value: v, label: v, isLoading: true };
    }));

    async function fetchOptions(inputValue: string) {
        const params = new URLSearchParams({
            q: inputValue,
            limit: '-1', // No limit!
        });
        normalizedValues.forEach(v => params.append('currentValue', v))

        let result: AsyncSelectOption[];
        try {
            const response = await fetch(`${fetchUrl}?${params}`);
            if (response.status >= 400) {
                // Cheap-n-dirty way to get to the catch block.
                throw new Error(`Failed to load options: ${response.status} ${response.statusText}`);
            }

            result = await response.json() as AsyncSelectOption[];
        } catch (e) {
            const props = {
                fetchUrl,
                params: params.toString(),
            };
            log.error("Failed to load options", { exception: e, ...props });
            appInsights.trackException({ exception: e }, props);
            result = [];
        }

        // Build the list of available options.
        // Start with the currently-selected options.
        // We can't remove those because if we do, they will disappear from the list and appear as though they aren't selected anymore.
        const options = [ ...currentOptions ];

        // Now, merge in fetched options.
        for (const fetchedOption of result) {
            const existingOption = options.find(o => o.value === fetchedOption.value);

            // If we already have the option, update it from the server.
            // The option may have been synthetically created from the initial value (which won't have a label or image).
            if (existingOption) {
                Object.assign(existingOption, fetchedOption);
                existingOption.isLoading = false;
            }
            else {
                // Otherwise, add the option
                options.push(fetchedOption);
            }
        }

        // Any remaining options that are still loading should be marked as errors.
        for (const option of options) {
            if (option.isLoading) {
                option.isError = true;
                option.isLoading = false;
            }
        }

        // Prepend any expression options, unless already included
        return [
            ...(expressionOptions || []).filter(e => !currentOptions.find(o => o.value === e.value)),
            ...options
        ];
    }

    function selectionChanged(selection: PropsValue<AsyncSelectOption>) {
        // Normalize the selection into an array
        const normalizedSelection = Array.isArray(selection) ? selection : [selection];
        setCurrentOptions(normalizedSelection);
        if (isSingleValue(selection)) {
            onChange(selection.value)
        } else {
            onChange(selection.map(s => s.value));
        }
    }

    function formatOption(option: AsyncSelectOption) {
        if (option.isError) {
            return <div className="flex flex-row items-center gap-2">
                <Icon icon="fa fa-circle-x" />
                <span>{option.value}</span>
            </div>
        }
        if (option.isLoading) {
            return <div className="flex flex-row items-center gap-2">
                <Icon icon="fa fa-spinner fa-spin-pulse" />
                <span>Loading…</span>
            </div>
        }

        const label = option.label && option.label.length > 0
            ? (<>{option.label}</>)
            : (<span className="italic text-gray-500">private channel ({option.value})</span>);

        return (
            <div>
                <div className="flex flex-row items-center gap-2">
                    {option.image && <img src={option.image} className="w-6 h-6 rounded-full" alt="" />}
                    <span className={option.isExpression ? 'italic' : null}>{label}</span>
            </div>
            {option.context && (
                <div className="text-xs font-normal opacity-80">{option.context}</div>
            )}
        </div>)
    }

    const loadOptions = AwesomeDebouncePromise(fetchOptions, 250);

    return (
        <PropertyEditor>
            <PropertyLabel stepProperty={property}>
                <AsyncSelect defaultOptions={true}
                             placeholder={placeholder}
                             isMulti={property.type.allowMultiple}
                             value={currentOptions}
                             required={!!property.required}
                             onChange={option => { selectionChanged(option); }}
                             formatOptionLabel={formatOption}
                             loadOptions={loadOptions}/>
            </PropertyLabel>
        </PropertyEditor>
    );
}

export function ChannelPropertyEditor(props: StepPropertyProps<string>) {
    return <AsyncSelectPropertyEditor
        {...props}
        fetchUrl="/api/internal/rooms/typeahead"
        expressions={PredefinedExpressions.getChannelPropertyExpressions()} />
}

export function MessageTargetPropertyEditor(props: StepPropertyProps<string>) {
    return <AsyncSelectPropertyEditor
        {...props}
        fetchUrl="/api/internal/rooms/typeahead"
        expressions={PredefinedExpressions.getMessageTargetPropertyExpressions()} />
}

export function MemberPropertyEditor(props: StepPropertyProps<string>) {
    return <AsyncSelectPropertyEditor
        {...props}
        fetchUrl="/api/internal/members/typeahead"
        expressions={PredefinedExpressions.getMemberPropertyExpressions()} />
}

export function EmojiPropertyEditor(props: StepPropertyProps<string>) {
    return <AsyncSelectPropertyEditor
        {...props}
        placeholder="Search for an emoji…"
        fetchUrl="/api/internal/emoji/typeahead"
        expressions={PredefinedExpressions.getMemberPropertyExpressions()} />
}
