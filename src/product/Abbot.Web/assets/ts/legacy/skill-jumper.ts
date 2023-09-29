import Combobox from '@github/combobox-nav'

export default function setupSkillJumper(input: HTMLInputElement) {
    if (!input) {
        return;
    }
    
    const url = input.form.action;
    if (!url) {
        console.log('The skill jumper input is not in a form with an action set.');
        return;
    }
    const list = input.nextElementSibling as HTMLUListElement;
    if (!list) {
        console.log('Next sibling must be the list.')
        return;
    }
    input.dataset.expanded = null;

    const combobox = new Combobox(input, list);
    
    input.addEventListener('keydown', event => {
        if (!['ArrowDown', 'ArrowUp'].includes(event.key) || !list.hidden) return
        combobox.navigate(event.key === 'ArrowDown' ? 1 : -1)
    });

    input.addEventListener('input', async () => {
        await fetchSuggestions();
        toggleList();
    });
    
    function toggleList() {
        const hidden = input.value.length === 0
        if (hidden) {
            combobox.stop();
            input.classList.remove('expanded');
            input.dataset.expanded = null;
        } else {
            if (!input.dataset.expanded) {
                combobox.start();
                input.classList.add('expanded');
                input.dataset.expanded = "true";
            }
        }
        list.hidden = hidden
    }

    list.addEventListener('combobox-commit', function(event) {
        if (event.target instanceof HTMLElement) {
            document.location.href = `/skills/${event.target.textContent}`;
        }
    })
    
    async function fetchSuggestions() {
        const q = input.value;
        const response = await fetch(`${url}?q=${q}`, {
            method: 'GET',
            headers: {
                'X-Requested-With': 'XmlHttpRequest',
                'Content-Type': 'multipart/form-data'
            },
        });
        const responseText = await response.text();
        if (response.ok) {
            list.innerHTML = responseText;
        }
    }
}