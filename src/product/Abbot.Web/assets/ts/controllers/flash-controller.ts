import { Controller } from "@hotwired/stimulus";
import { HubConnection, HubConnectionBuilder, ILogger, LogLevel } from "@microsoft/signalr";
import { getMetaValue } from "../env";
import logger from "../log";

const signalRLog = logger("signalr");
const log = logger("flash");

export interface Flash {
    name: string,
    arguments: unknown[],
}

const liveHost = getMetaValue("abbot-live-host");
const flashUrl = liveHost
    ? `https://${liveHost}/flash`
    : "/live/flash";

class SignalRLogger implements ILogger {
    log(logLevel: LogLevel, message: string): void {
        switch (logLevel) {
            case LogLevel.Critical:
            case LogLevel.Error:
                signalRLog.error(message);
                break;
                signalRLog.error(message);
                break;
            case LogLevel.Warning:
                signalRLog.warn(message);
                break;
            case LogLevel.Information:
                signalRLog.log(message);
                break;
        }
    }
}

/*
 * Attaches the target element to the "Flash" hub.
 * Messages received by the client on that Hub will be dispatched as custom events.
 */
export default class extends Controller {
    static values = {
        groups: String,
    };
    declare hasGroupsValue: boolean;
    declare groupsValue: string;
    hub: HubConnection;
    flashHandler?: (flash: Flash) => void;

    async connect() {
        this.hub = new HubConnectionBuilder()
            .withUrl(flashUrl)
            .withAutomaticReconnect()
            .configureLogging(new SignalRLogger())
            .build();
        await this.hub.start();
        log.log("Connected to '%s'", flashUrl);

        this.hasConnected(this.hub.connectionId || "");
        this.hub.onreconnected((newId) => this.hasConnected(newId || ""));
    }

    async disconnect() {
        if (this.flashHandler) {
            this.hub.off("dispatchFlash", this.flashHandler);
        }
        this.flashHandler = undefined;

        // Stopping the connection should properly remove us from groups.
        await this.hub.stop();
    }

    private async hasConnected(connectionId: string) {
        this.flashHandler = (flash: Flash) => {
            log.log("Dispatching flash:%s", flash.name, flash.arguments);
            this.dispatch(flash.name, { detail: flash.arguments });
        };
        log.log("Binding to 'dispatchFlash' method");
        this.hub.on('dispatchFlash', this.flashHandler);

        if (this.hasGroupsValue) {
            for (const group of this.groupsValue.split(",")) {
                await this.joinGroup(group);
            }
        }

        log.log("Connected to flash hub with connection ID '%s'", connectionId);
    }

    private async joinGroup(group: string) {
        try {
            const result = await this.hub.invoke<boolean>("JoinGroup", group)
            if (result) {
                log.log("Joined flash group '%s'", group);
            }
        } catch (e) {
            log.error("Failed to join flash group '%s'", group, e);
        }
    }
}