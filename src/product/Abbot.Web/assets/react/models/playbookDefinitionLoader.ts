import {PlaybookDefinition} from "../../ts/api/internal";
import logger from "../../ts/log";
const log = logger("PlaybookDefinitionLoader");

export default class PlaybookDefinitionLoader {
    constructor(private definitionSource: HTMLInputElement, private webhookTriggerUrlSource: HTMLScriptElement) {
    }

    public load(): PlaybookDefinition {
        return this.loadPlaybookDefinition(this.definitionSource);
    }

    public update(definition: PlaybookDefinition): void {
        const oldJson = this.definitionSource.value;
        const newJson = JSON.stringify(definition);

        if (oldJson === newJson) {
            log.verbose('Skipping save of Playbook as no changes detected');
            return;
        }

        log.verbose('Saving Playbook', { old: oldJson, new: newJson });
        // Submit as unpublished
        this.definitionSource.value = newJson;
        this.definitionSource.form.requestSubmit();
    }

    public get webhookTriggerUrl(): string {
        return this.webhookTriggerUrlSource.innerHTML.trim();
    }

    private loadPlaybookDefinition(source: HTMLInputElement) : PlaybookDefinition {
        const playbookDefinitionJson = source?.value;
        if (playbookDefinitionJson && playbookDefinitionJson.length > 2) {
            const definition = JSON.parse(playbookDefinitionJson);
            // Reserialize for stable dirty detection
            source.value = JSON.stringify(definition);
            return definition;
        }
        const playbookDefinition = {
            formatVersion: 0,
            triggers: [],
            startSequence: 'start_sequence',
            sequences: {
                'start_sequence': {
                    actions: [],
                }
            }
        };
        source.innerHTML = JSON.stringify(playbookDefinition);
        return playbookDefinition;
    }
}
