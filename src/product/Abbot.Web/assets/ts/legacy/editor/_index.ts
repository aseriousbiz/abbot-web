import codeEditor from './code';
import 'codemirror/mode/javascript/javascript';
import 'codemirror/mode/python/python';
import 'codemirror/mode/clike/clike';
import 'codemirror/mode/diff/diff';
import 'codemirror/mode/mllike/mllike';
import 'codemirror/addon/comment/comment';
import 'codemirror/addon/edit/matchbrackets';
import 'codemirror/addon/edit/closebrackets';
import 'codemirror/addon/hint/show-hint';
import 'codemirror/addon/hint/javascript-hint';
import 'codemirror/addon/hint/anyword-hint';
import 'codemirror/addon/lint/lint';
import 'codemirror/addon/lint/javascript-lint';
import 'codemirror/addon/search/search';
import 'codemirror/addon/search/searchcursor';
import 'codemirror/addon/search/jump-to-line';
import 'codemirror/addon/dialog/dialog';
import createShell from './shell';
import getLanguage from './languages';
import Mousetrap from "mousetrap";

export default function(formElement: HTMLFormElement) {
    const document = formElement.ownerDocument;
    const MARK_ERROR_CLASS_NAME = 'CodeMirror-lint-mark-error';
    const skillNameInput = document.getElementById('skillName') as HTMLInputElement;
    const argumentsHiddenInput = document.getElementById('arguments') as HTMLInputElement;
    const editorTextarea = formElement.querySelector(".code-editor") as HTMLTextAreaElement;
    const saveButton = document.getElementById('saveButton');
    const languageKey = editorTextarea.dataset.language || 'CSharp';
    const skillId = editorTextarea.dataset.skillId;
    const language = getLanguage(languageKey);
    const recoveredChanges = document.getElementById('recovered-changes');

    let markers = []; // Track error markers

    // Handle save from anywhere in the skill editor
    document.addEventListener("keydown", function(e) {
        if ((window.navigator.platform.match("Mac") ? e.metaKey : e.ctrlKey) && e.key === 's') {
            e.preventDefault();
            if (saveButton) {
                saveButton.click();
            }
        }
    }, false);

    if (skillNameInput) {
        skillNameInput.addEventListener('change', (e: Event) => {
            const target = e.target as HTMLInputElement;
            target.value = target.value.toLowerCase().replace(' ', '-');
        });
    }

    const editor = codeEditor(editorTextarea, language);

    Mousetrap.bindGlobal('shift+escape', () => {
        editor.setOption('fullScreen', false);
    });

    if (typeof(Storage) !== "undefined") {
        const storedValue = localStorage.getItem(`skill:${skillId}:value`);
        const currentValue = editor.getValue();
        if (storedValue && storedValue !== currentValue) {
            recoveredChanges.style.display = 'block';

            // Recover Changes
            const recoverChangesButton = document.getElementById('recover-changes');
            if (!recoverChangesButton) {
                console.log('Recovered changes (id: recover-changes) button missing.');
            }
            else {
                recoverChangesButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    console.log('Setting doc with stored value:\n' + storedValue);
                    editorTextarea.value = storedValue;
                    editor.setValue(storedValue);
                    localStorage.removeItem(`skill:${skillId}:value`);
                    recoveredChanges.style.display = 'none';
                });
            }

            // Ignore Changes
            const ignoreChangesButton = document.getElementById('ignore-changes');
            if (!ignoreChangesButton) {
                console.log('Recovered changes (id: ignore-changes) button missing.');
            }
            else {
                ignoreChangesButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    localStorage.setItem(`skill:${skillId}:value`, editor.getValue());
                    recoveredChanges.style.display = 'none';

                });
            }
        }
        else {
            localStorage.removeItem(`skill:${skillId}:value`);
        }
    } else {
        console.warn('Localstorage not supported');
    }

    editor.focus();

    if (skillId) {
        editor.on('change', () => {
            // Update local storage.
            if (typeof(Storage) !== "undefined") {
                localStorage.setItem(`skill:${skillId}:value`, editor.getValue());
                recoveredChanges.style.display = 'none';

            } else {
                console.warn('Localstorage not supported');
            }
        });
    }

    function isFullScreen(e) {
        return window.navigator.platform.match("Mac")
            ? e.metaKey && e.key === 'm' // CMD+M
            : (e.keyCode || e.which) === 122; // F11
    }

    const deviceShortcut = document.querySelector('#fullscreen-shortcut .device');
    if (deviceShortcut) {
        deviceShortcut.addEventListener('click', e => {
            e.preventDefault();
            editor.setOption('fullScreen', true);
        });
    }

    const shortcutClose = document.getElementById('fullscreen-close');
    if (shortcutClose) {
        shortcutClose.addEventListener('click', e => {
            e.preventDefault();
            editor.setOption('fullScreen', false);
        });
    }

    // Handle fullscreen
    document.addEventListener("keydown", function(e) {
        if (isFullScreen(e)) {
            e.preventDefault();
            editor.setOption('fullScreen', !editor.getOption('fullScreen'));
        }
    }, false);

    function clearMarkers() {
        // Clear out any error indicators
        markers.forEach(marker => marker.clear());
    }

    function reportError(error) {
        const parsedError = language.parseError(error);

        const lineStart = parsedError.lineStart;
        const lineEnd = parsedError.lineEnd;
        const spanStart = parsedError.spanStart;
        const spanEnd = parsedError.spanEnd;

        const from = {line: lineStart, ch: spanStart};
        const to = {line: lineEnd, ch: spanEnd};
        const foundMarkers = editor.findMarks(from, to);

        shell.response(parsedError.errorMessage, "scriptError");


        if (foundMarkers.some(m => m.className && m.className.includes('CodeMirror-lint-mark-error'))) {
            // There's already a marker for this error (likely because of sharpmirror).
            return;
        }

        // Create marker and Tooltip.
        const marker = editor.markText(from, to, {className: `${MARK_ERROR_CLASS_NAME} abboterror err_${error.errorId}`});
        markers.push(marker); // Track all the error markers that have been created so they can be cleared later.

        // Not every error has enough information to report into the code window. Make sure it exists before
        // adding the tooltip.
        const newMarkers = document.getElementsByClassName("err_" + error.errorId);

        if (newMarkers && languageKey !== "JavaScript") {
            for (const newMarker of newMarkers) {
                newMarker.appendChild(createTooltipElement(error.description));
            }
        }
    }

    function createTooltipElement(description) {
        const container = document.createElement('div');
        container.className = 'CodeMirror-lint-tooltip cm-s-vscode-dark tooltiptext';
        const tooltip = document.createElement('div');
        tooltip.className = 'CodeMirror-lint-message-error';
        tooltip.textContent = description;
        container.appendChild(tooltip);
        return container;
    }

    async function exec(endpoint, cmd, callback) {
        let ps = shell.PARSE_STATUS.OK;
        if (cmd.length) {
            clearMarkers();

            let args = cmd.join("\n");
            const skillName = skillNameInput.value.toLowerCase();
            const skillPrefix = `.${skillName} `;
            if (args.toLowerCase().startsWith(skillPrefix)) {
                args = args.substr(skillPrefix.length);
            }
            argumentsHiddenInput.value = args;

            editor.save(); // Updates the textarea with the current code in the code editor.
            let code = editorTextarea.value;

            /*
            * We wrap the user code for JavaScript skills in an async function for the hinter, and so users
            * understand how we run their code. Since we want this to work a specific way in the runner, we
            * don't want to actually send the wrapper. This removes the first and last lines of the code
            * before sending it for evaluation.
             */
            if (language.mode === 'text/javascript') {
                let code_lines = code.split("\n");
                code = code_lines.slice(1,-1).join("\n");
            }

            try {
                const response = await fetch(endpoint, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        name: skillName,
                        arguments: args,
                        code: code,
                        language: languageKey
                    })
                });

                if (response.ok) {
                    const replies = await response.json();
                    shell.response(replies + "\n\n");
                }
                else {
                    const errors = await response.json();
                    if (Array.isArray(errors)) {
                        errors.forEach(e => reportError(e));
                    } else {
                        reportError(errors);
                    }
                    ps = shell.PARSE_STATUS.ERR;
                    shell.response(response.statusText + ':' + response.status);
                }
            } catch (e) {
                ps = shell.PARSE_STATUS.ERR;
                shell.response(e);
            }

            callback.call(this, { parsestatus: ps });
        }
    }

    const shell = createShell(exec, skillNameInput, false);
}
