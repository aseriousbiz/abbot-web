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

export interface BoldOptions {
    HTMLAttributes: Record<string, unknown>,
}

declare module '@tiptap/core' {
    interface Commands<ReturnType> {
        bold: {
            /**
             * Set a bold mark
             */
            setBold: () => ReturnType,
            /**
             * Toggle a bold mark
             */
            toggleBold: () => ReturnType,
            /**
             * Unset a bold mark
             */
            unsetBold: () => ReturnType,
        }
    }
}

export const starInputRegex = /(?:^|\s)(\*(.+?)\*)$/
export const starPasteRegex = /(?:^|\s)(\*(.+?)\*)/g

export const MrkdwnBold = Mark.create<BoldOptions>({
    name: 'bold',

    addOptions() {
        return {
            HTMLAttributes: {},
        }
    },

    parseHTML() {
        return [
            {
                tag: 'strong',
            },
            {
                tag: 'b',
                getAttrs: node => (node as HTMLElement).style.fontWeight !== 'normal' && null,
            },
            {
                style: 'font-weight',
                getAttrs: value => /^(bold(er)?|[5-9]\d{2,})$/.test(value as string) && null,
            },
        ]
    },

    renderHTML({ HTMLAttributes }) {
        return ['strong', mergeAttributes(this.options.HTMLAttributes, HTMLAttributes), 0]
    },

    addCommands() {
        return {
            setBold: () => ({ commands }) => {
                return commands.setMark(this.name)
            },
            toggleBold: () => ({ commands }) => {
                return commands.toggleMark(this.name)
            },
            unsetBold: () => ({ commands }) => {
                return commands.unsetMark(this.name)
            },
        }
    },

    addKeyboardShortcuts() {
        return {
            'Mod-b': () => this.editor.commands.toggleBold(),
            'Mod-B': () => this.editor.commands.toggleBold(),
        }
    },

    addInputRules() {
        return [
            markInputRule({
                find: starInputRegex,
                type: this.type,
            }),
        ]
    },

    addPasteRules() {
        return [
            markPasteRule({
                find: starPasteRegex,
                type: this.type,
            }),
        ]
    },
})