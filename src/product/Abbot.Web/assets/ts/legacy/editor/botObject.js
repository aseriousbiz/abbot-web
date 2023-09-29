/**
 * Mocks up the Request object sent to the Skill Runner for purposes of autocomplete.
 **/

function javaScriptRequest() {
    return {
        "id": "AFakeId",
        "skillId": 44,
        "skillName": "ASkill",
        "userName": "abbot",
        "arguments": "rem @abbot is a bot",
        "mentions": [
            {
                "id": "U0123121", 
                "name": "A Person", 
                "userName": "aperson",
                "timeZone": "America/Los_Angeles",
                "location": {
                    "formattedAddress": "1234 Main Street",
                    "coordinate": {
                        "latitude": 0,
                        "longitude": 0
                    }
                },
                "platformType": 1,
            }],
        "reply": function(response) { return response },
        "replyLater": function(response, delay_in_seconds) { return response },
        "replyWithButtons": function(response, buttons) { return response },
        "from": {
            "id": "U09999999",
            "userName": "sampleuser",
            "name": "An Example User",
            "timeZone": "America/Los_Angeles",
            "location": {
                "formattedAddress": "1234 Main Street",
                "coordinate": {
                    "latitude": 0,
                    "longitude": 0
                }
            },
            "platformType": 1
        },
        "brain": {
            "get": function (key) {
                return "a value;"
            },
            "write": function (key, value) {
                return "a value"
            },
            "list": function () {
                return ["value1", "value2", "value3"];
            },
            "delete": function (key) {
                return "deleted key"
            },
        },
        "secrets": {
            "get": function (key) {
                return "a secret";
            },
        },
        "utils": {
            "geocode": function (address, includeTimezone=false) {
                    return {"coordinate": 
                            {
                                "formattedAddress": "123 Main Street",
                                "latitude": 1, 
                                "longitude": 1,
                                "timeZone": "America/Los_Angeles"
                            }}
                }
            },
        "tokenizedArguments": [
                {"value": "that", "originalText": "that"}, 
                {"value": "@abbot", "OriginalText": "@abbot"}, 
                {"value": "is", "OriginalText": "is"}, 
                {"value": "a", "OriginalText": "a"}, 
                {"value": "crazy", "OriginalText": "crazy"}, 
                {"value": "bot", "OriginalText": "bot"}
            ],
        "isInteraction": false,
        "isRequest": false,
        "request": { // note, this is not completely implemented in the runner
            "isForm": false,
            "isJson": false,
            "httpMethod": null,
            "rawBody": null,
            "contentType": null,
            "headers": [{"key1": "value"}, {"key2": "value"}],
            "query": [{"key1": "value"}, {"key2": "value"}],
            "form": [{"key": "value"}, {"key2": "value"}]
        },
        "httpTriggerEvent": false,
        };
}

function pythonRequest() {
    return {};
}

export default function botObject(language) {
    switch (language) {
        case "javascript":
            return javaScriptRequest();
        case "python":
            return pythonRequest();
        default:
            return javaScriptRequest();
    }
}