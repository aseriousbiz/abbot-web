import {Editor} from "@tiptap/react";
import {MenuButton} from "./menuButton";
import * as React from "react";

/*
 * Provides action buttons for the rich text editor to insert emoji, mentions, etc.
 */
export function ActionBar(props: { editor: Editor }) {
    const editor = props.editor;

    if (!editor) {
        return null
    }

    return (
        <div className="flex gap-x-1" onClick={
            // For some reason, clicking within the editor propagated the click event to the first button in
            // the menu bar. This prevents that.
            e => e.preventDefault()}>
            <MenuButton
                onClick={() => editor.chain().focus().insertContent(':').run()}
                disabled={false}
                tooltip="Insert Emoji"
                isActive={editor.isActive('emoji')}
            >
                <i className="fa-regular fa-face-smile"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().insertContent('@').run()}
                disabled={false}
                tooltip="Insert Mention"
                isActive={editor.isActive('mention')}
            >
                <i className="fa-regular fa-at"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().insertContent('#').run()}
                disabled={false}
                tooltip="Insert Channel"
                isActive={editor.isActive('channel')}
            >
                <i className="fa-regular fa-hashtag"></i>
            </MenuButton>
            <MenuButton
                onClick={() => editor.chain().focus().insertContent('{').run()}
                disabled={false}
                tooltip="Insert Handlebars Template"
                isActive={editor.isActive('handlebars')}
            >
                <i className="fa-regular fa-brackets-curly"></i>
            </MenuButton>
        </div>
    )
}
