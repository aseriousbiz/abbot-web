// ******** STOP HERE AND READ *********
// The code below is legacy JavaScript, from before we used Stimulus.
// Over time, we can port it over, but for now, we don't want to add more to it.
// Please don't add new things here.
// Instead, create Stimulus Controllers in './ts/controllers'.
// ******** STOP HERE AND READ *********

import '@github/markdown-toolbar-element'
import '@github/tab-container-element'

import autosize from '@github/textarea-autosize'
import createSkillEditor from './editor/_index';
import setupModals from './modals';
import setupCronEditor from './cron/editor';
import setupCronTranslators from './cron/translator';
import codeEditor from './editor/code';
import getLanguage from "./editor/languages";
import setupFullscreen from './editor/fullscreen';
import Mousetrap from 'mousetrap';
import 'mousetrap/plugins/global-bind/mousetrap-global-bind';
import setupSkillJumper from './skill-jumper'
import flatpickr from "flatpickr";
import confirmDatePlugin from 'flatpickr/dist/plugins/confirmDate/confirmDate'

import { installOnLoad } from './install';
import { staffOnLoad } from './staff';
import { adminOnLoad } from './admin';
import { navbarOnLoad } from './navbar';
import { insightsOnLoad } from './insights-charts';
import { ajaxPartialOnLoad } from './ajax-partial';
import { conversationChartsOnLoad } from './conversation-charts';

export function legacyOnLoad() {
    // Run the loaders for the other pages, since we're all in one bundle now.
    installOnLoad();
    staffOnLoad();
    adminOnLoad();
    navbarOnLoad();
    insightsOnLoad();
    ajaxPartialOnLoad();
    conversationChartsOnLoad();

    setupFullscreen();

    document.querySelectorAll<HTMLTextAreaElement>('textarea').forEach(textarea => {
        autosize(textarea);

        // Sets up CMD + Enter to submit the form.
        textarea.addEventListener('keydown', e => {
            const key = e.which || e.keyCode;
            if (key === 13 && e.metaKey) {
                textarea.form.requestSubmit();
            }
        });
    })

    const editorForm = document.getElementById('editorForm') as HTMLFormElement;
    if (editorForm) {
        createSkillEditor(editorForm);
    }

    const code = document.querySelector<HTMLTextAreaElement>('textarea.code-viewer[data-readonly=true]');
    if (code) {
        const language = getLanguage(code.dataset.language || 'CSharp');
        const editor = codeEditor(code, language);

        if (code.classList.contains('recovered-changes')) {
            // Show recovered changes from localstorage
            const storedValue = localStorage.getItem(`skill:${code.dataset.skillId}:value`);
            if (storedValue) {
                editor.setValue(storedValue);
            }
            else {
                // There are no changes.
                editor.getWrapperElement().style.display = 'none';
                document.getElementById('no-changes').style.display = 'block';
            }
        }
    }

    document.querySelectorAll<HTMLInputElement>('input[type=checkbox][data-enable]').forEach((e: HTMLInputElement) => {
        const target = document.querySelector<HTMLInputElement>(`#${e.dataset.enable}`);
        if (target) {
            e.addEventListener('click', () => {
                if (e.checked) {
                    target.readOnly = false;
                    target.classList.remove("disabled");
                } else {
                    target.readOnly = true;
                    target.classList.add("disabled");
                }
            });
        }
    });

    const modals = setupModals();
    const cronEditor = document.querySelector<HTMLElement>('#cron-editor');
    setupCronEditor(cronEditor, modals);
    setupCronTranslators();

    // Set up keyboard shortcuts for modals.
    document.querySelectorAll<HTMLElement>('.modal[data-hotkey]').forEach(modal => {
        const hotkey = modal.dataset.hotkey;
        if (hotkey) {
            const hotkeys = hotkey.split(',');

            hotkeys.forEach(key => {
                const bind = key.startsWith('ctrl') || key.startsWith('command')
                    ? Mousetrap.bindGlobal
                    : Mousetrap.bind;

                bind(key, () => {
                    if (modals.active) {
                        modals.active.classList.remove('is-active');
                    }

                    const firstInput = modal.querySelector<HTMLInputElement>('input[type=text]');
                    if (firstInput) {
                        setTimeout(() => {
                            firstInput.value = '';
                            firstInput.focus();
                        }, 1);
                    }
                    modals.active = modal;
                    modal.classList.add('is-active');
                    modal.querySelectorAll<HTMLElement>('[aria-label="close"]')
                        .forEach(btn => {
                            btn.addEventListener('click', e => {
                                modal.classList.remove('is-active');
                                e.preventDefault();
                            });
                        });
                });
            });
        }
    })

    document.querySelectorAll<HTMLAnchorElement>('a[data-hotkey]').forEach(link => {
        if (link.dataset.hotkey) {
            const hotkey = link.dataset.hotkey.indexOf(',') > -1
                ? link.dataset.hotkey.split(',')
                : [link.dataset.hotkey];
            Mousetrap.bind(hotkey, () => {
                link.click();
            });
        }
    });

    Mousetrap.bindGlobal('escape', () => {
        if (modals.active) {
            modals.active.classList.remove('is-active');
            modals.active = null;
        }

        // If we're in a regular form element, we want to be able to escape
        // it so we can use the skill jumper. But we don't want this if we're
        // in the skill editor because we may be escaping an auto-complete prompt.
        if (!document.activeElement.closest('div.CodeMirror') && document.activeElement instanceof HTMLInputElement) {
            document.activeElement.blur();
        }
    });

    setupSkillJumper(document.querySelector<HTMLInputElement>('#skill-jumper-input'));

    /* Set up the date range picker */
    // If we use more than one picker on a page, things get weird.
    // TODO: Put this in a component or something.
    flatpickr(".date-range", {
        mode: "range",
        enableTime: true,
        allowInput: true,
        "plugins": [confirmDatePlugin({})],
        onClose: [
            (_dates, _currentDateString, self) => {
                self.input.form.submit();
            }
        ]
    });

    const clearDateRangeButton = document.querySelector('button[data-clear]') as HTMLButtonElement;
    if (clearDateRangeButton) {
        clearDateRangeButton.addEventListener('click', function (e) {
            e.preventDefault();
        }, false);
    }

    const isMacOs = window.navigator.platform.match("Mac");
    const hideClass = isMacOs ? "pc" : "macos";
    document.querySelectorAll<HTMLElement>(`.${hideClass}`).forEach(el => {
        el.style.display = 'none';
    });

    const ctrlOrCmd = isMacOs ? "Cmd" : "Ctrl";
    document.querySelectorAll<HTMLElement>('.cmd-or-ctrl').forEach(el => {
        el.innerText = ctrlOrCmd;
    });

    // Tabbed navigation
    // Based off of https://codepen.io/FlorinPop17/pen/ZZajGB
    const navTabs = document.querySelectorAll<HTMLElement>(".js-tabbed-navigation-item");
    const navContents = document.querySelectorAll<HTMLElement>(".js-tabbed-navigation-content");

    navTabs.forEach(clickedTab => {
        // Add onClick event listener on each tab
        clickedTab.addEventListener('click', event => {
            if (!(event.currentTarget instanceof HTMLElement)) {
                return;
            }

            // Remove the active class from all the tabs (this acts as a "hard" reset)
            navTabs.forEach(tab => {
                tab.classList.remove('tabnav-btn-active');
            });

            // Get what's in the "nav-for" data attribute
            const contentTarget = event.currentTarget.dataset.navFor;
            // match with the section's "content-for-nav" data attribute
            const elem = document.getElementById(contentTarget);
            // Hide all info sections
            navContents.forEach(content => {
                content.classList.add('hidden');
            });
            // Then show the one that's selected
            elem.classList.remove("hidden", "invisible");
            // Add the active class on the clicked tab
            clickedTab.classList.add('tabnav-btn-active');
        });
    });
}
