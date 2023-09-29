/*
 Copyright (c) 2023, Tiptap GmbH
 Adapted from the MIT licensed extension in https://github.com/ueberdosis/tiptap
 */
import {
    Mark,
    markInputRule,
    markPasteRule,
    mergeAttributes,
} from '@tiptap/core'

export interface ItalicOptions {
    HTMLAttributes: Record<string, unknown>,
}

declare module '@tiptap/core' {
    interface Commands<ReturnType> {
        italic: {
            /**
             * Set an italic mark
             */
            setItalic: () => ReturnType,
            /**
             * Toggle an italic mark
             */
            toggleItalic: () => ReturnType,
            /**
             * Unset an italic mark
             */
            unsetItalic: () => ReturnType,
        }
    }
}

export const underscoreInputRegex = /(?:^|\s)(_(.+?)_)$/
export const underscorePasteRegex = /(?:^|\s)(_(.+?)_)/g

export const MrkdwnItalic = Mark.create<ItalicOptions>({
    name: 'italic',

    addOptions() {
        return {
            HTMLAttributes: {},
        }
    },

    parseHTML() {
        return [
            {
                tag: 'em',
            },
            {
                tag: 'i',
                getAttrs: node => (node as HTMLElement).style.fontStyle !== 'normal' && null,
            },
            {
                style: 'font-style=italic',
            },
        ]
    },

    renderHTML({ HTMLAttributes }) {
        return ['em', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes), 0]
    },

    addCommands() {
        return {
            setItalic: () => ({ commands }) => {
                return commands.setMark(this.name)
            },
            toggleItalic: () => ({ commands }) => {
                return commands.toggleMark(this.name)
            },
            unsetItalic: () => ({ commands }) => {
                return commands.unsetMark(this.name)
            },
        }
    },

    addKeyboardShortcuts() {
        return {
            'Mod-i': () => this.editor.commands.toggleItalic(),
            'Mod-I': () => this.editor.commands.toggleItalic(),
        }
    },

    addInputRules() {
        return [
            markInputRule({
                find: underscoreInputRegex,
                type: this.type,
            }),
        ]
    },

    addPasteRules() {
        return [
            markPasteRule({
                find: underscorePasteRegex,
                type: this.type,
            }),
        ]
    },
})