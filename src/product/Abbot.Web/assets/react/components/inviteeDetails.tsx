import * as React from "react";
import {PropertyEditor} from "./propertyEditors/propertyEditor";
import {Label} from "./propertyEditors/formControls";

const webhookPayloadExample = `{
  "customer": {
    "email": "somebody@example.com"
  }
}`;

export function InviteeDetails() {
    return (
        <PropertyEditor>
            <Label className="flex flex-col">
                <div>
                    Invitee
                </div>
            </Label>
            <p className="font-normal text-sm text-gray-500">
                The invitee is supplied via the <code>Customer Info Submitted</code> trigger.
            </p>
            <div className="font-normal text-xs text-gray-500">
                <p>Example:</p>
                <pre><code className="text-xs">{webhookPayloadExample}</code></pre>
            </div>
        </PropertyEditor>
    );
}