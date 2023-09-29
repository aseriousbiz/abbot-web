import Clipboard from "react-clipboard.js";
import {Tooltip} from "react-tooltip";
import * as React from "react";
import {Step} from "../models/step";
import {PlaybookDefinition} from "../models/playbookDefinition";

export default function CustomerInfoDetails(props: { playbook: PlaybookDefinition, isStaffMode: boolean, step: Step }){
    const { playbook} = props;

    function onCopied(e) {
        e.trigger.innerText = "Copied!"
    }

    const exampleBody =
        `{
  "customer": {
    "name": "My Profitable Business",
    "email": "contact@example.com",
    "segments": ["EMEA", "Enterprise"]
  }
}`;

    return (
        <div>
            <div className="bg-slate-50 rounded p-1">
                <div className="flex items-center p-1">
                    <p className="font-medium text-slate-500 text-xs select-none">POST/PUT</p>
                    <Clipboard className="btn btn-sm ml-auto" onSuccess={onCopied} data-clipboard-text={playbook.webhookTriggerUrl}>
                        Copy
                    </Clipboard>
                </div>
                <Tooltip id="webhook-url" />
                <input readOnly={true}
                       data-tooltip-id="webhook-url"
                       data-tooltip-content={playbook.webhookTriggerUrl}
                       className="whitespace-pre-wrap text-sm text-slate-700 font-mono w-full p-1 bg-transparent has-tooltip has-tooltip-arrow has-tooltip-multiline"
                       value={playbook.webhookTriggerUrl} />
            </div>

            <details className="rounded p-2 bg-slate-50 mt-2">
                <summary className="text-xs font-medium text-slate-500 hover:cursor-pointer hover:text-slate-700">More info</summary>
                <div className="text-sm text-gray-700">
                    <p className="mt-1 px-2">
                        This URL is unique to this Playbook and is used to trigger the Playbook. There are two ways to
                        submit customer information.
                    </p>

                    <p className="mt-1 px-2">
                        <strong>Form post</strong><br />
                        The first is to <a href={playbook.webhookTriggerUrl}>visit the URL in a browser</a> and submit the form.
                    </p>

                    <p className="mt-1 px-2">
                        <strong>JSON</strong><br />
                        The second is to make an HTTP POST request to this URL with the content type <code>application/json</code>
                        with the following body.
                    </p>

                    <pre className="bg-indigo-50 rounded p-1"><code className='text-xs'>{exampleBody}</code></pre>
                </div>
            </details>
        </div>

    );
}