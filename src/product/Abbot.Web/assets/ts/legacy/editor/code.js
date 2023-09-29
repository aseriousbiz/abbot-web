import CodeMirror from 'codemirror';
import mirrorsharp from 'mirrorsharp';
import {JSHINT} from "jshint";
import botObject from "./botObject";
import 'codemirror-mode-ink';

/**
 * @param {string} mode - The code language mode.
 * @param {boolean} readOnly - Whether the editor should be readonly.
 * @returns {EditorConfiguration}
 *
 * CREDIT: smart tabbing ("Tab", "Backspace", "Shift-Tab") from here: https://github.com/codemirror/CodeMirror/issues/988#issuecomment-556928041
 */
function getCodeMirrorOptions(mode, readOnly) {
    let hint = false;
    let lint = false;

    if (mode === "text/javascript") {
        lint = { options: { esversion: 9 }};
        hint = CodeMirror.hint.javascript;
    }

    return {
        lineNumbers: true,
        mode: mode,
        theme: "vscode-dark",
        electricChars: true,
        indentWithTabs: false,
        smartIndent: true,
        matchBrackets: true,
        autoCloseBrackets: true,
        readOnly: readOnly,
        nocursor: readOnly,
        lint: lint,
        hint: hint,
        extraKeys: {
            "Tab": cm => {
                if (cm.getMode().name === 'null') {
                    cm.execCommand('insertTab');
                } else {
                    if (cm.somethingSelected()) {
                        cm.execCommand('indentMore');
                    } else {
                        cm.execCommand('insertSoftTab');
                    }
                }
            },
            "Backspace": cm => {
                if (!cm.somethingSelected()) {
                    let cursorsPos = cm.listSelections().map((selection) => selection.anchor);
                    let indentUnit = cm.options.indentUnit;
                    let shouldDelChar = false;
                    for (let cursorIndex in cursorsPos) {
                        let cursorPos = cursorsPos[cursorIndex];
                        let indentation = cm.getStateAfter(cursorPos.line).indented;
                        if (!(indentation !== 0 &&
                            cursorPos.ch > 0 &&
                            cursorPos.ch <= indentation &&
                            cursorPos.ch % indentUnit === 0)) {
                            shouldDelChar = true;
                        }
                    }
                    if (!shouldDelChar) {
                        cm.execCommand('indentLess');
                    } else {
                        cm.execCommand('delCharBefore');
                    }
                } else {
                    cm.execCommand('delCharBefore');
                }
            },
            "Shift-Tab": cm => {
                cm.execCommand('indentLess');
            },
            "Cmd-/": cm => {
                cm.execCommand('toggleComment');
            },
            "Ctrl-/": cm => {
                cm.execCommand('toggleComment');
            },
            "Ctrl-R": cm => {
                cm.execCommand('replace')
            },
            "Shift-Ctrl-R": cm => {
                cm.execCommand('replaceAll')
            },
            "Shift-Cmd-P": cm => {
                cm.execCommand('findPersistent')
            },
            "Shift-Ctrl-P": cm => {
                cm.execCommand('findPersistent')
            }
        }
    };
}

/**
 * Sets up the mirrorsharp or codemirror editor and returns an instance of the
 * codemirror editor.
 *
 * @param {HTMLTextAreaElement} textarea - The textarea element that contains the code to edit..
 * @param {Language} language - The code language object.
 *
 * CREDIT: Adapted from https://stackoverflow.com/a/155812
 */
export default function(textarea, language) {
    let editor;
    let sharpEditor;

    const readOnly = textarea.dataset.readonly || false;
    if (language.mode === 'text/x-csharp' && !readOnly) {
        const window = window || textarea.ownerDocument.defaultView
        const serviceUrl = window.location.href.replace(/^http(s?:\/\/[^/]+).*$/i, 'ws$1/mirrorsharp');

        sharpEditor = mirrorsharp(textarea, {
            serviceUrl: serviceUrl,
            forCodeMirror: getCodeMirrorOptions(language.mode, readOnly)
        });
        editor = sharpEditor.getCodeMirror();
    }
    else {
        // Load up JSHINT for JS mode (has to happen before the editor is loaded).
        if (language.mode === 'text/javascript') {
            window.JSHINT = JSHINT;
            window.bot = botObject();
        }

        editor = CodeMirror.fromTextArea(textarea,
            getCodeMirrorOptions(language.mode, readOnly));

        // Load a mocked up object for JavaScript autocomplete and linting.
        // This has to happen after the editor is loaded.
        // We don't need to do any of this if the viewer is in readOnly mode since users won't be able to modify.
        if (!readOnly && language.mode === 'text/javascript') {
            // Enable Autocomplete for JavaScript
            // CREDIT: https://stackoverflow.com/a/54377763/122117
            editor.on("inputRead", function(instance) {
                if (instance.state.completionActive) {
                    return;
                }
                let cur = instance.getCursor();
                let token = instance.getTokenAt(cur);
                if (token.type && token.type !== "comment") {
                    CodeMirror.commands.autocomplete(instance);
                }
            });

            // JavaScript skills are wrapped in an async function.
            // To make it clearer to the end-user, display the wrapper code, but don't allow them to change it.
            editor.doc.addLineClass(0, "wraps", "code-placeholder");
            editor.markText({line:0, ch:0}, {line:1, ch:0}, {readOnly: true});

            // In order for this approach to work, we also have to do this for the last line.
            let lastLine = editor.doc.lastLine();
            editor.doc.addLineClass(lastLine, "wraps", "code-placeholder");
            editor.markText({line: lastLine, ch:0}, {line: lastLine+1, ch: 80}, {readOnly: true})

            // Don't allow changes in the first line of the editor
            editor.on('beforeChange',function(cm, change) {
                if (change.origin === 'setValue') {
                    return;
                }
                if (change.from.line === 0 ||
                    (change.from.line === editor.doc.lastLine()) &&
                    (change.from.ch > 0)) {
                    change.cancel();
                }
            });
        }
    }

    // We don't need to update the textarea on every change, but we definitely need to do it on blur.
    // This ensures the textarea is up to date when someone tries to navigate away and we need to do our
    // change detection.
    editor.on('blur', editor.save);
    return editor;
}

