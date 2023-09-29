import { Application } from "@hotwired/stimulus";
import logger from "./log";

const log = logger("Autocontrollers");

type SelectorMappings = {
    [selector: string]: string | string[];
}

function bindControllers(elem: Element, controllerNames: string | string[]) {
    controllerNames = Array.isArray(controllerNames) ? controllerNames : [controllerNames];
    const controllerAttribute = elem.getAttribute('data-controller');
    const controllerList = controllerAttribute === null ? [] : controllerAttribute.split(' ');
    let modified = false;
    controllerNames.forEach(controllerName => {
        if (!controllerList.includes(controllerName)) {
            modified = true;
            controllerList.push(controllerName);
        }
    });

    // Only set the attribute if we modified it. This prevents an infinite loop by not triggering a mutation if we've already bound the controllers.
    if (modified) {
        elem.setAttribute("data-controller", controllerList.join(" "));
    }
}

function attachControllersToElement(elem: Element, mappings: SelectorMappings) {
    for (const selector in mappings) {
        // Check the element itself.
        if (elem.matches(selector)) {
            bindControllers(elem, mappings[selector]);
        }
        
        // Check any child nodes.
        elem.querySelectorAll(selector).forEach(e => {
            bindControllers(e, mappings[selector]);
        });
    }
}

function attachControllers(nodes: Node | NodeList, mappings: SelectorMappings) {
    if (nodes instanceof NodeList) {
        nodes.forEach(node => {
            if (node instanceof Element) {
                attachControllersToElement(node, mappings);
            }
        })
    } else if (nodes instanceof Element) {
        attachControllersToElement(nodes, mappings);
    }
}

/**
 * Enables "Auto Controllers" on the provided Stimulus application.
 * Elements that match the provided selectors with have the provided controllers automatically bound to them.
 * NOTE: Controllers attached by this method will not be automatically removed if the element is changed to no longer match the selector.
 * 
 * @param app The app to enable auto controllers on.
 */
export function useAutocontrollers(app: Application) {
    const selectorMappings: SelectorMappings = {};
    app.router.modules.forEach(module => {
        const autoBindings = module.controllerConstructor["__autoBind"] as string[] | undefined;
        if (autoBindings) {
            autoBindings.forEach(selector => {
                log.verbose(`Binding ${selector} to Stimulus controller ${module.identifier}`);
                selectorMappings[selector] = module.identifier;
            });
        }
    });

    // Set up a mutation observer for the root element
    const observer = new MutationObserver(mutations => {
        // Bind controllers to any new tags
        mutations.forEach(mutation => {
            if (mutation.type === "childList") {
                attachControllers(mutation.addedNodes, selectorMappings);
            } else if (mutation.type === "attributes") {
                attachControllers(mutation.target, selectorMappings);
            }
        })
    });
    observer.observe(app.element, {
        childList: true,
        subtree: true,
        attributes: true,
    });

    // Now that we're watching for changes, autobind everything else we see.
    attachControllersToElement(app.element, selectorMappings);
}

export function autobind(...args: string[]): (constructor: new (...args: unknown[]) => unknown) => void {
    return ctor => {
        const list = ctor['__autoBind'] || [];
        list.push(...args);
        ctor['__autoBind'] = list;
    };
}