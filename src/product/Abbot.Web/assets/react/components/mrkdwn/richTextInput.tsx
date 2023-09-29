import {EditorContent, Extensions, JSONContent, useEditor} from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import {MrkdwnBold} from "./mrkdwnBold";
import {MrkdwnItalic} from "./mrkdwnItalic";
import {FormattingBar} from "./formattingBar";
import {MrkdwnStrike} from "./mrkdwnStrike";
import {Link} from "@tiptap/extension-link";
import {ActionBar} from "./actionBar";

export interface RichTextInputProps<TReadOnly = boolean> {
    className?: string,
    defaultValue?: JSONContent,
    value?: TReadOnly extends true ? JSONContent : never,
    onChange?: TReadOnly extends true ? never : (value: JSONContent) => void,
    readOnly?: TReadOnly,
    rows?: number,
    extensions?: Extensions,
    required?: boolean,
}

export default function RichTextInput({ className, defaultValue, value, onChange, readOnly, rows, extensions, required }: RichTextInputProps) {
    const editor = useEditor({
        extensions: [
            StarterKit.configure({
                // Disable some extensions in favor of our extended ones.
                bold: false,
                italic: false,
                strike: false,
                hardBreak: false,
                paragraph: {
                    HTMLAttributes: {
                        class: 'px-1 -py-1 bg-white font-normal',
                    },
                },
                bulletList: {
                    HTMLAttributes: {
                        class: 'list-disc list-inside space-y-1',
                    }
                },
                orderedList: {
                    HTMLAttributes: {
                        class: 'list-decimal list-inside space-y-1',
                    }
                },
                code: {
                    HTMLAttributes: {
                        class: 'text-[#D1486D] mx-0 px-0',
                    },
                },
                blockquote: {
                    HTMLAttributes: {
                        class: 'border-l-4 border-gray-200 pl-2 ml-1',
                    }
                }
            }),
            Link.configure({
                openOnClick: false,
            }),
            //Handlebars,
            MrkdwnBold,
            MrkdwnItalic,
            MrkdwnStrike,
            ...(extensions || []),
        ],
        editorProps: {
            attributes: {
                class: className,
                style: getEditorStyle(rows),
            },
        },
        // triggered on every change
        onUpdate: ({ editor }) => {
            const json = editor.getJSON();
            onChange(json);
        },

        content: readOnly ? value : defaultValue,

        editable: !readOnly,
    }, [readOnly, value]);

    const isEmpty = editor ? editor.isEmpty : true;

    return (
        <div className="border border-slate-300 rounded p-1 flex flex-col gap-y-1">
            {!readOnly && (
                <FormattingBar editor={editor} />
            )}
            <EditorContent editor={editor} className="rich-text-editor" onClick={
                // For some reason, clicking within the editor propagated the click event to the first button in
                // the menu bar. This prevents that.
                e => e.preventDefault()} />
            {/* This is a fancy little trick to show HTML validation messages for the Rich Text Editor, which isn't an input tag */}
            {required && <input defaultValue={isEmpty ? "" : "non-empty"} required className="opacity-0 float-left pointer-events-none -mt-6 w-full" tabIndex={-1} />}
            {!readOnly && (
                <>
                    <ActionBar editor={editor} />
                </>
            )}
        </div>
    )
}

function getEditorStyle(rows?: number) {
    // If row is 1 or less, ignore the height.
    // A value of 1 leads to clipping of the action bar below it.
    return rows > 1 ? `min-height: ${rows}rem;` : null;
}
