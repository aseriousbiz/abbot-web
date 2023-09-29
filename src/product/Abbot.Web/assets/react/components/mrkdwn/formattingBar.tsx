import {Editor} from "@tiptap/react";
import {MenuButton} from "./menuButton";
import * as React from "react";
import {useCallback} from "react";

/*
 * Provides formatting buttons for the rich text editor.
 */
export function FormattingBar(props: { editor: Editor }) {
    const editor = props.editor;

    const setLink = useCallback(() => {
        const previousUrl = editor.getAttributes('link').href
        const url = window.prompt('URL', previousUrl)

        // cancelled
        if (url === null) {
            return
        }

        // empty
        if (url === '') {
            editor.chain()
                .focus()
                .extendMarkRange('link')
                .unsetLink()
                .run()

            return
        }

        // update link
        editor.chain()
            .focus()
            .extendMarkRange('link')
            .setLink({ href: url })
            .run()
    }, [editor])

    if (!editor) {
        return null
    }

    return (
        <div className="flex gap-x-1" onClick={
                // For some reason, clicking within the editor propagated the click event to the first button in
                // the menu bar. This prevents that.
                e => e.preventDefault()}>
            <MenuButton
                onClick={() => editor.chain().focus().toggleBold().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleBold()
                        .run()
                }
                tooltip="Bold"
                isActive={editor.isActive('bold')}
            >
                <i className="fa-solid fa-bold"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleItalic().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleItalic()
                        .run()
                }
                tooltip="Italic"
                isActive={editor.isActive('italic')}
            >
                <i className="fa-solid fa-italic"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleStrike().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleStrike()
                        .run()
                }
                tooltip="Strike through"
                isActive={editor.isActive('strike')}
            >
                <i className="fa-solid fa-strikethrough"></i>
            </MenuButton>
            <MenuButton
                onClick={() => setLink()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .extendMarkRange('link')
                        .run()
                }
                tooltip="Link"
                isActive={editor.isActive('link')}
            >
                <i className="fa-solid fa-link"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleOrderedList().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleOrderedList()
                        .run()
                }
                tooltip="Ordered list"
                isActive={editor.isActive('orderedList')}
            >
                <i className="fa-solid fa-list-ol"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleBulletList().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleBulletList()
                        .run()
                }
                tooltip="Bulleted list"
                isActive={editor.isActive('bulletList')}
            >
                <i className="fa-solid fa-list"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleBlockquote().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleBlockquote()
                        .run()
                }
                tooltip="Blockquote"
                isActive={editor.isActive('blockquote')}
            >
                <i className="fa-regular fa-block-quote"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleCode().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleCode()
                        .run()
                }
                tooltip="Code"
                isActive={editor.isActive('code')}
            >
                <i className="fa-solid fa-code"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().toggleCodeBlock().run()}
                disabled={
                    !editor.can()
                        .chain()
                        .focus()
                        .toggleCodeBlock()
                        .run()
                }
                tooltip="Code block"
                isActive={editor.isActive('codeBlock')}
            >
                <i className="fa-regular fa-square-code"></i>
            </MenuButton>
        </div>
    )
}
