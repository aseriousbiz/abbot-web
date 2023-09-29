import {Mention} from "@tiptap/extension-mention";
import createSuggestion from "../mrkdwn/suggestion";
import {mergeAttributes} from "@tiptap/core";
import getHandlebarsSuggestions from "./handlebar-suggestion";
import { Extensions } from "@tiptap/react";
import {ExpressionOption} from "../../models/options";

export function createTipTapExtensions(options: ExpressionOption[]): Extensions {
    const handlebarSuggestions = getHandlebarsSuggestions(options);

    return [
        Mention
            .extend({
                name: "mention"
            })
            .configure({
                HTMLAttributes: {
                    class: 'bg-yellow-100 text-blue-700 border border-gray-200 rounded-md p-0.5',
                },
                renderLabel({ options, node }) {
                    return `${options.suggestion.char}${node.attrs.label || node.attrs.id}`;
                },
                suggestion: createSuggestion('mention', '@'),
            }),
        Mention
            .extend({
                name: "channel"
            })
            .configure({
                HTMLAttributes: {
                    class: 'bg-blue-100 text-blue-700 border border-gray-200 rounded-md p-0.5',
                },
                renderLabel({ options, node }) {
                    return `${options.suggestion.char}${node.attrs.label || node.attrs.id}`;
                },
                suggestion: createSuggestion('channel', '#'),
            }),
        Mention
            .extend({
                name: "emoji",
                renderHTML({ node, HTMLAttributes }) {
                    return node.attrs.label && node.attrs.label.startsWith('http')
                        ? [
                            'img',
                            mergeAttributes({ 'data-type': this.name, 'src': node.attrs.label, 'class': 'w-5 h-5 inline-block' }, this.options.HTMLAttributes, HTMLAttributes),
                        ]
                        : [
                            'span',
                            mergeAttributes({ 'data-type': this.name }, this.options.HTMLAttributes, HTMLAttributes),
                            this.options.renderLabel({
                                options: this.options,
                                node,
                            }),
                        ]
                }
            })
            .configure({
                renderLabel({ node }) {
                    return `${node.attrs.label || node.attrs.id}`;
                },
                suggestion: createSuggestion('emoji', ':'),
            }),
        Mention
            .extend({
                name: "handlebars",
                renderHTML({ node, HTMLAttributes }) {
                    return [
                        'code',
                        mergeAttributes(
                            { 'data-tooltip': `{{ ${node.attrs.id} }}` },
                            this.options.HTMLAttributes,
                            HTMLAttributes),
                        [
                            'code',
                            { 'class': 'text-gray-400 text-xs px-0' },
                            '{{'
                        ],
                        [
                            'span',
                            { 'class': 'px-1' },
                            this.options.renderLabel({
                                options: this.options,
                                node,
                            })
                        ],
                        [
                            'code',
                            { 'class': 'text-gray-400 text-xs px-0' },
                            '}}'
                        ]
                    ];
                }
            })
            .configure({
                HTMLAttributes: {
                    class: "bg-slate-100 text-slate-700 border border-gray-200 rounded-md px-0 py-0.5 text-xs has-tooltip-arrow",
                },
                renderLabel({ node }) {
                    return node.attrs.label || node.attrs.id;
                },
                suggestion: handlebarSuggestions,
            }),
    ]
}