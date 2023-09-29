/*
Install.js - This is only used on the installation complete page to check if installation is indeed complete.
 */

export async function installOnLoad() {
    const confirming = document.querySelector<HTMLElement>('#confirming-install');
    if (!confirming) {
        return;
    }
    const platformId = confirming.dataset.platformId;

    function hideConfirming() {
        confirming.style.display = 'none';
    }

    function reloadPage() {
        const form = document.querySelector<HTMLFormElement>('form#force-reload');
        if (form) {
            form.submit();
        }
    }

    // The polling function. Credit: https://davidwalsh.name/javascript-polling
    async function poll(fn, timeout, interval) {
        const endTime = Number(new Date()) + (timeout || 5000);
        interval = interval || 100;

        const checkCondition = async function(resolve, reject) {
            // If the condition is met, we're done!
            const result = await fn();
            if (result) {
                resolve(result);
            }
            // If the condition isn't met but the timeout hasn't elapsed, go again
            else if (Number(new Date()) < endTime) {
                setTimeout(checkCondition, interval, resolve, reject);
            }
            // Didn't match and too much time, reject!
            else {
                // eslint-disable-next-line prefer-rest-params
                reject(new Error('timed out for ' + fn + ': ' + arguments));
            }
        };

        return new Promise(checkCondition);
    }

    async function isBotInstalled() {
        const response = await fetch('/api/internal/installation?platformId=' + encodeURI(platformId));
        if (!response.ok) {
            document.getElementById('unexpected-error').style.display = 'block';
            hideConfirming();
            throw new Error('Response status was not ok: ' + response.status)
        }
        const result = await response.json();
        return result.botInstalled;
    }

    try {
        await poll(async () => await isBotInstalled(), 10000, 1000);
        reloadPage();
    }
    catch (e) {
        // Timeout.
        document.getElementById('confirmation-failed').style.display = 'block';
        hideConfirming();
    }
}