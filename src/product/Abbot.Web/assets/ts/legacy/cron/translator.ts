export default function setupCronTranslators() {
    document.querySelectorAll<HTMLInputElement>('[data-cron]')
        .forEach((description: HTMLElement) => {
            const source = document.getElementById(description.dataset.cron) as HTMLInputElement;
            if (source instanceof HTMLInputElement) {
                source.addEventListener('input', e => {
                    if (!(e.target instanceof HTMLInputElement)) {
                        return;
                    }

                    fetch('/api/internal/cron?cron=' + encodeURI(e.target.value))
                        .then(async response => {
                            if (response.ok) {
                                const body = await response.json();
                                const text = body.description;

                                if (!body.success) {
                                    description.classList.add('has-error');
                                }
                                else {
                                    description.classList.remove('has-error');
                                }

                                if ('value' in description) {
                                    description.value = text;
                                } else if ('innerText' in description) {
                                    description.innerText = text;
                                } else {
                                    alert(text);
                                }
                            }
                        });
                });

                if (source.value !== '') {
                    source.dispatchEvent(new CustomEvent('input'));
                    source.dispatchEvent(new CustomEvent('change'));
                }
            }
        });
}
