// Incoming values could be a serialized tiptap document or a plain mrkdwn string if it was created before
// this change was made. If it's a plain string, we need to convert it to a tiptap document.
import {JSONContent} from "@tiptap/react";

export function convertToTipTapDocument(value: JSONContent | string) : JSONContent {
    if (!value) {
        return null;
    }

    if (typeof value === 'object') {
        return value; // Must be a tiptap document.
    }

    // Handle legacy values that may be plain strings

    const stringValue = value as string;

    if (value[0] === '{') {
        try {
            return JSON.parse(stringValue);
        } catch {
            // HMM, maybe it's mrkdwn that just happened to start with a curly brace?
            // We'll fall through and assume that.
        }
    }

    return {
        type: 'doc',
        content: [
            {
                type: 'paragraph',
                content: [
                    {
                        type: 'text',
                        text: stringValue,
                    },
                ],
            }
        ],
    };
}