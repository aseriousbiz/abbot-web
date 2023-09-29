import {Cron} from "./cron";

function selectValue(value: string, element?: HTMLSelectElement) {
    if (!element) {
        return;
    }

    const option = element.querySelector<HTMLOptionElement>(`[value='${value}']`);
    if (option) {
        option.selected = true;
        element.dispatchEvent(new CustomEvent('change'));
    }
}

export default function setupCronEditor(editorElement, modals) {
    let shownField = null;
    
    // Handle showing/hiding sections of the cron editor based on
    // the recurrence type chosen.
    const recurrence = document.getElementById('recurrence') as HTMLSelectElement;
    if (recurrence) {
        recurrence.addEventListener('change', e => {
            if (e.target instanceof HTMLSelectElement) {
                const field = document.getElementById(e.target.value);
                if (shownField) {
                    shownField.classList.add('is-hidden');
                    shownField = null;
                }
                if (field) {
                    shownField = field;
                    field.classList.remove('is-hidden');
                }
            }
        });
        recurrence.dispatchEvent(new CustomEvent('change'));
    }

    // Methods used to populate the cron editor based on the recurrence type selected.
    function selectCron(element, selector, cron) {
        const valueElements = element.querySelectorAll(selector);
        valueElements.forEach(valueElement => {
            if (valueElement) {
                selectValue(cron.schedule, valueElement);
            }
        });
    }
    
    const recurrenceValueSetters = {
        weekly: function(element: HTMLElement, cron: Cron) {
            const weekdays = {};
            cron.weekdays.forEach(w => weekdays[`* * * * ${w}`] = true);
            
            const dayCheckboxes = element.querySelectorAll<HTMLInputElement>('input.modal-value[type=checkbox]');
            [...dayCheckboxes].forEach(cb => {
                cb.checked = weekdays[cb.value] || false;
            });
        },
        
        cron: function(element, cron) {
            const valueElement = element.querySelector('input.modal-value');
            if (valueElement) {
                valueElement.value = cron.schedule;
                valueElement.dispatchEvent(new CustomEvent('input'));
                valueElement.dispatchEvent(new CustomEvent('change'));
            }
        }
    };

    // Set the initial value of the cron editor.
    modals.initializers['cron-editor'] = function initialize(modal, update) {
        if (recurrence) {
            let cron = null;
            try {
                cron = new Cron(update.value);
            }
            catch {
                cron = Cron.never();
            }
            const hourCron = cron.time;
            const monthDay = cron.monthDay;
            selectCron(editorElement, 'select.cron-month', monthDay);
            selectCron(editorElement, 'select.cron-time', hourCron);
            
            const cronInput = editorElement.querySelector('#cron-schedule');
            if (cronInput) {
                cronInput.value = update.value;
                cronInput.dispatchEvent(new CustomEvent('input'));
                cronInput.dispatchEvent(new CustomEvent('change'));
            }
            
            const recurrenceType = cron.recurrenceType;
            const recurrencePane = editorElement.querySelector(`#${recurrenceType}`);
            if (recurrencePane) {
                const setter = recurrenceValueSetters[recurrenceType];
                if (setter) {
                    setter(recurrencePane, cron);
                }
            }
            selectValue(recurrenceType, recurrence);
        }
    }
    
    // Set up the cron editor modal callback.
    modals.callbacks['cron-editor'] = function successCallback() {
        const modalValues = shownField.querySelectorAll('.modal-value');
        const visibleValues = [...modalValues]
            .filter(e => e.type !== 'checkbox' || e.checked);
        let modalValue;
        if (visibleValues.length === 0) {
            throw 'There are no visible inputs with the css class `modal-value`.';
        }
        
        if (visibleValues.length > 1) {
            const cron = visibleValues
                .map(v => new Cron(v.value))
                .reduce((a, c) => a.combine(c));
            modalValue = {value: cron.schedule};
        } else {
            modalValue = visibleValues[0];
        }
        const value = modalValue.value || modalValue.innerText;
        
        // Make sure there's no errors.
        const description = editorElement.querySelector('.cron-description');
        const success = description && !description.classList.contains('has-error');
        
        return { success: success, value: value };
    };
}