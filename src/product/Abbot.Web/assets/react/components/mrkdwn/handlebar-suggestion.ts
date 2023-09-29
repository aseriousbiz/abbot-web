import { ReactRenderer } from '@tiptap/react'
import tippy from 'tippy.js'
import HandlebarList from './handlebarList'
import { PluginKey } from "prosemirror-state";
import { SuggestionOptions } from '@tiptap/suggestion';
import logger from '../../../ts/log';
import {ExpressionOption} from "../../models/options";

const log = logger('handlebar-suggestion');

function isMatch(option: ExpressionOption, query: string) {
    if (!query || query === '') {
        return true;
    }

    // Try matching on the expression
    if (option.expression.startsWith(query)) {
        return true;
    }

    // Try matching on the name
    return option.label.includes(query);
}

/**
 * Generates a "suggestions" object for use by the Mention tiptap extension based on the provided list of available {@link HandlebarExpression} objects.
 * @param options The options representing available handlebars expressions in the current context.
 * @returns A "suggestions" object for use by the Mention tiptap extension.
 */
export default function getHandlebarsSuggestions(options: ExpressionOption[]): Omit<SuggestionOptions, 'editor'> {
    function fetchHandlebarOptions(query: string): ExpressionOption[] {
        // Strip a leading '{' and leading/trailing whitespace
        query = query.startsWith("{") ? query.slice(1).trim() : query.trim();

        log.verbose(`Finding handlebars match for ${query}.`, {query});

        return [
            { value: query, label: query, expression: query },
            ...options.filter(option => isMatch(option, query)),
        ];
    }

    return {
        char: '{',
        pluginKey: new PluginKey("handlebars"),
        allowSpaces: true,
        items: ({ query }) => {
            return fetchHandlebarOptions(query);
        },
        render: () => {
            let component;
            let popup;

            return {
                onStart: props => {
                    component = new ReactRenderer(HandlebarList, {
                        props,
                        editor: props.editor,
                    })

                    if (!props.clientRect) {
                        return
                    }

                    popup = tippy('body', {
                        getReferenceClientRect: props.clientRect,
                        appendTo: () => document.body,
                        content: component.element,
                        showOnCreate: true,
                        interactive: true,
                        trigger: 'manual',
                        placement: 'bottom-start',
                    })
                },

                onUpdate(props) {
                    if (!component) {
                        // When debouncing, we might not have this yet.
                        return;
                    }
                    component.updateProps(props)

                    if (!props.clientRect) {
                        return
                    }

                    popup[0].setProps({
                        getReferenceClientRect: props.clientRect,
                    })
                },

                onKeyDown(props) {
                    if (!component) {
                        // When debouncing, we might not have this yet.
                        return;
                    }
                    if (props.event.key === 'Escape') {
                        popup[0].hide()

                        return true
                    }

                    return component.ref?.onKeyDown(props)
                },

                onExit() {
                    if (!popup || !component) {
                        return;
                    }
                    popup[0].destroy()
                    component.destroy()
                },
            }
        },
    };
}
