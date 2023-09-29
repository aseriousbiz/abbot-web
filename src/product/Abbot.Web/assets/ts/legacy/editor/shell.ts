import Shell from 'cmjs-shell';
import CodeMirror from "codemirror";

/**
 * Sets up and returns the Abbot shell console.
 * @param exec - The callback when a command is invoked in the console.
 * @param {HTMLInputElement?} skillNameInput - The name of the skill to exec.
 * @param giveFocus - whether or not to give the shell focus on load.
 * @returns {Shell}
 */
export default function(exec, skillNameInput : HTMLInputElement, giveFocus) {
    const skillName = (skillNameInput) ? skillNameInput.value + ' ' : '';

    const containerId = 'console';

    // eslint-disable-next-line no-undef
    const containerElement = document.getElementById(containerId);
    const endpoint = containerElement.dataset.endpoint;
    const version = (containerElement) ? containerElement.dataset.version : '0.0.1';

    const options = {
        container: `#${containerId}`,
        lineWrapping: true,
        initial_prompt: `=> @abbot ${skillName}`,
        exec_function: exec.bind(null, endpoint),
        suppress_initial_scroll: !giveFocus
    };
    const shell = new Shell(CodeMirror, options);

    let banner = `           _     _           _    
          | |   | |         | |   
      __ _| |__ | |__   ___ | |_  
     / _\` | '_ \\| '_ \\ / _ \\| __| 
    | (_| | |_) | |_) | (_) | |_  
     \\__,_|_.__/|_.__/ \\___/ \\__| 
 Abbot v${version}. Go on, say something... 

`;

    shell.setOption("theme", "the-matrix");
    shell.response(banner, null, null, true /* no scroll */);
    if (giveFocus) {
        shell.focus();
    }

    if (skillNameInput) {
        skillNameInput.addEventListener('change', (evt: Event) => {
            const target = evt.target as HTMLInputElement;
            if (!target.classList.contains('input-validation-error')) {
                shell.replacePrompt(`=> @abbot ${target.value} `);
            }
        });
    }
    return shell;
}
