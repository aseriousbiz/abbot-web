import { SeverityLevel } from "@microsoft/applicationinsights-web";
import { isDevelopment } from "./env"
import { appInsights } from "./telemetry";

const loggingStorageKey = "abbot:logging";

export function loggingEnabled() {
    // Logging is always on in development.
    if (isDevelopment) {
        return true;
    }

    // Check local storage
    if (window.localStorage.getItem(loggingStorageKey) === "true") {
        return true;
    }
    return false;
}

export function setLogging(enabled: boolean) {
    if (enabled) {
        window.localStorage.setItem(loggingStorageKey, "true");
    } else {
        window.localStorage.removeItem(loggingStorageKey);
    }
}

// Install 'setLogging' and 'loggingEnabled' on the window object so they can be used easily from the console.
window['setLogging'] = setLogging;
window['loggingEnabled'] = loggingEnabled;

export interface Logger {
    isEnabled(): boolean;
    verbose(message: string, ...args: unknown[]): void

    log(message: string, keys: Record<string, unknown>): void
    log(message: string, ...args: unknown[]): void
    warn(message: string, keys: Record<string, unknown>): void
    warn(message: string, ...args: unknown[]): void
    error(message: string, keys: Record<string, unknown>): void
    error(message: string, ...args: unknown[]): void
    critical(message: string, keys: Record<string, unknown>): void
    critical(message: string, ...args: unknown[]): void
    groupCollapsed(groupTitle?: string, ...args: unknown[]): void
    groupEnd(): void
    child(childCategory: string): Logger;
}

export class CompositeLogger implements Logger {
    constructor(private loggers: Logger[]) {
    }
    
    isEnabled(): boolean {
        return this.loggers.some(l => l.isEnabled());
    }

    verbose(message: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.verbose(message, ...args));
    }
    log(message: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.log(message, ...args));
    }
    warn(message: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.warn(message, ...args));
    }
    error(message: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.error(message, ...args));
    }
    critical(message: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.error(message, ...args));
    }
    groupCollapsed(groupTitle?: string, ...args: unknown[]): void {
        this.loggers.forEach(l => l.groupCollapsed(groupTitle, ...args));
    }
    groupEnd(): void {
        this.loggers.forEach(l => l.groupEnd());
    }
    child(childCategory: string): Logger {
        const children = this.loggers.map(l => l.child(childCategory));
        return new CompositeLogger(children);
    }
}

export class ConsoleLogger implements Logger {
    constructor(private category: string, private console: Console) {
    }

    isEnabled() {
        return loggingEnabled();
    }

    child(childCategory: string) {
        return new ConsoleLogger(`${this.category}:${childCategory}`, this.console);
    }

    verbose(message: string, ...args: unknown[]): void {
        if (loggingEnabled()) {
            this.console.log(this.formatMessage(message), ...args);
        }
    }

    log(message: string, ...args: unknown[]): void {
        if (loggingEnabled()) {
            this.console.log(this.formatMessage(message), ...args);
        }
    }

    warn(message: string, ...args: unknown[]): void {
        this.console.warn(this.formatMessage(message), ...args);
    }

    error(message: string, ...args: unknown[]): void {
        this.console.error(this.formatMessage(message), ...args);
    }

    critical(message: string, ...args: unknown[]): void {
        this.console.error(this.formatMessage(message), ...args);
    }

    groupCollapsed(groupTitle?: string, ...args: unknown[]): void {
        this.console.groupCollapsed(groupTitle, ...args);
    }
    groupEnd(): void {
        this.console.groupEnd();
    }

    formatMessage(message: string): string {
        return `[${this.category}]: ${message}`;
    }
}

export class ApplicationInsightsLogger implements Logger {
    constructor(private category: string) {
    }

    isEnabled(): boolean {
        return !!appInsights;
    }

    child(childCategory: string) {
        return new ApplicationInsightsLogger(`${this.category}:${childCategory}`);
    }

    verbose(message: string, ...args: unknown[]): void {
        this.emitTrace(SeverityLevel.Verbose, message, args);
    }

    log(message: string, ...args: unknown[]): void {
        this.emitTrace(SeverityLevel.Information, message, args);
    }

    warn(message: string, ...args: unknown[]): void {
        this.emitTrace(SeverityLevel.Warning, message, args);
    }

    error(message: string, ...args: unknown[]): void {
        this.emitTrace(SeverityLevel.Error, message, args);
    }

    critical(message: string, ...args: unknown[]): void {
        this.emitTrace(SeverityLevel.Critical, message, args);
    }

    // eslint-disable-next-line @typescript-eslint/no-empty-function, @typescript-eslint/no-unused-vars
    groupCollapsed(groupTitle?: string, ...args: unknown[]): void {
    }

    // eslint-disable-next-line @typescript-eslint/no-empty-function
    groupEnd(): void {
    }

    private emitTrace(severityLevel: SeverityLevel, message: string, args: unknown[]) {
        appInsights?.trackTrace({
            message: message,
            severityLevel: severityLevel,
        }, { category: this.category, ...this.processArgs(args) });
    }

    private processArgs(args: unknown[]): { [key: string]: unknown } {
        const allArgs: unknown[] = [];
        const dict: Record<string, unknown> = {};
        for (const arg of args) {
            if (typeof arg === "object") {
                // If the arg is an object, just merge it into the dict.
                Object.assign(dict, arg);
            }
            allArgs.push(arg);
        }

        // Add the raw args as a property.
        // This captures:
        // * Non-object args
        // * Object args that have a property name that ends up overriding the property name of an existing arg.
        dict["rawArgs"] = allArgs;
        return dict;
    }
}

export const RootLogger = new CompositeLogger([
    new ConsoleLogger("Abbot", console),
    new ApplicationInsightsLogger("Abbot")
]);

export default function logger(name: string) {
    return RootLogger.child(name);
}