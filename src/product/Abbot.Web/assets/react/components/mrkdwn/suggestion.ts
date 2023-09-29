import { ReactRenderer } from '@tiptap/react'
import tippy from 'tippy.js'
import MentionList, {MentionItem} from './mentionList'
import AwesomeDebouncePromise from 'awesome-debounce-promise';
import { PluginKey } from "prosemirror-state";

async function fetchMentions(query: string): Promise<MentionItem[]> {
    const response = await fetch(`/api/internal/members/find?q=${query}&limit=10`);
    if (!response.ok) {
        return [];
    }
    const json = await response.json();
    return json.map(item => ({
        id: item.platformUserId,
        label: item.nickName,
        avatar: item.avatarUrl,
    }));
}

async function fetchChannels(query: string): Promise<MentionItem[]> {
    const response = await fetch(`/api/internal/rooms/typeahead?q=${query}&limit=10`);
    if (!response.ok) {
        return [];
    }
    const json = await response.json();
    const channels = json.map(item => ({
        id: item.value,
        label: item.label,
    }));
    channels.unshift({ id: '{{ outputs.channel.id }}', label: 'Channel from outputs' })
    return channels;
}

async function fetchEmoji(query: string): Promise<MentionItem[]> {
    const response = await fetch(`/api/internal/emoji/search?q=${query}&limit=10`);
    if (!response.ok) {
        return [];
    }
    const json = await response.json();
    return json.map(item => ({
        id: item.name,
        label: item.imageUrl || item.emoji,
        emoji: true,
    }));
}

const debounceWait = 250;

const fetchMethods = {
    '@': {fetch: fetchMentions, load: AwesomeDebouncePromise(fetchMentions, debounceWait)},
    '#': {fetch: fetchChannels, load: AwesomeDebouncePromise(fetchChannels, debounceWait)},
    ':': {fetch: fetchEmoji, load: AwesomeDebouncePromise(fetchEmoji, debounceWait)},
};

export default function createSuggestion(pluginKey: string, char: string) {
    return {
        char: char,
        pluginKey: new PluginKey(pluginKey),
        items: ({query}) => {
            const methods = fetchMethods[char];
            // We can't debounce the first key press or shit will break.
            // After that, we're fine!
            // To clarify: the first key press, `@` (which sets the query to '') sets up completion menu.
            // Subsequent key presses start to make requests. What I found in practice is if I typed @a with debouncing,
            // the menu would never show up. By not debouncing the first key press, the menu works every time.
            if (query === '') {
                return methods.fetch(query);
            } else {
                return methods.load(query);
            }
        },

        render: () => {
            let component;
            let popup;

            return {
                onStart: props => {
                    component = new ReactRenderer(MentionList, {
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
    }
}