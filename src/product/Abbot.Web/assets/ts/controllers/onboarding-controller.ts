import {Controller} from "@hotwired/stimulus";

export default class extends Controller<HTMLElement> {
    static targets = ["input", "onboardingChoice", "hubName"]
    
    declare hubNameTarget
    
    updateName(event) {
        if (event.target.value === "InternalUsers") {
            this.hubNameTarget.setAttribute("value", "hub-internal")
        } else if (event.target.value === "ExternalCustomers") {
            this.hubNameTarget.setAttribute("value", "hub-customers")
        }
    }
}