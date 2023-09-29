import * as React from "react";
import {useEffect, useMemo} from "react";
import {ExtendedOption, Option} from "../../models/options";
import {StepPropertyWithExpressionsProps} from "./propertyEditor";
import Select from "react-select";
import {PredefinedExpressions} from "../../models/predefinedExpressions";
import usePlaybook from "../../hooks/usePlaybooks";

export function Label({ children, className, ...props }: React.HTMLProps<HTMLLabelElement>) {
    return (
        <label {...props}
            className={`text-gray-500 text-sm font-medium ${className || ''}`}>
            {children}
        </label>
    );
}

export interface EditorInputProps<E, V> extends Omit<React.HTMLProps<E>, 'onChange'> {
    onChange(value: V): void;
}

interface TextInputProps<T extends string> extends Omit<EditorInputProps<HTMLInputElement, string>, 'type'> {
    type?: T extends 'checkbox' | 'radio' | 'hidden' ? never : T,
}

export function TextInput<T extends string = string>({ className, onChange, ...props }: TextInputProps<T>) {
    // If 'value' is undefined, React will initialize any <input> tags as uncontrolled and get mad when we flip them to controlled later.
    // https://react.dev/reference/react-dom/components/input#im-getting-an-error-a-component-is-changing-an-uncontrolled-input-to-be-controlled
    // So, we set it to an empty string if it's undefined.
    // All of the editors we support here allow an empty string as a valid value, even 'number'.
    props.value = props.value ?? '';
    return (
        <input {...props}
               onChange={e => onChange(e.currentTarget.value)}
               className={`border border-slate-300 rounded-md p-1 bg-white shadow-inner ${className || ''}`} />
    );
}

export function DropDown({ children, className, selectClassName, onChange, ...props }: EditorInputProps<HTMLSelectElement, string> & { selectClassName?: string }) {
    return (
        <div className={`form-select-wrapper ${className || ''}`}>
            <select {...props}
                    onChange={e => onChange(e.currentTarget.value)}
                    className={`border border-slate-300 rounded-md bg-white shadow-inner form-select w-full ${selectClassName || ''}`}>
                {children}
            </select>
        </div>
    );
}



/*
 * A drop down that understands expressions.
 */
export function ExpressionSelect({ step, property, value, onChange, expressions }: StepPropertyWithExpressionsProps<string>) {
    const { playbook } = usePlaybook();

    const options = useMemo(
        () => PredefinedExpressions.mapExpressionsToOptions(
            expressions,
            playbook,
            step,
            false),
        [playbook, step]);

    const selectedOption = useMemo(
        () => options.find(o => o.value === value),
        [options, value]);

    useEffect(() => {
        // If there's no value, set it to the first option.
        if (!value) {
            const customerExpression = options[0];
            if (customerExpression) {
                onChange(customerExpression.value);
            }
        }
    }, [value]);

    function formatOption(option: ExtendedOption) {
        return (
            <div>
                <div className="flex flex-row items-center gap-2">
                    <span className={option.isExpression ? 'italic' : null}>{option.label}</span>
                </div>
                {option.context && (
                    <div className="text-xs font-normal opacity-80">{option.context}</div>
                )}
            </div>)
    }

    function handleChange(option: Option) {
        // Propagate the value and not the option.
        onChange(option.value);
    }

    return (
        <Select name={property.name}
                className="w-full"
                required={!!property.required}
                onChange={handleChange}
                value={selectedOption}
                formatOptionLabel={formatOption}
                options={options} />
    );
}