import { ApplicationInsights } from '@microsoft/applicationinsights-web'
import { getMetaValue, organizationId } from "./env";

// DO NOT attempt to access `logger` here. It creates a circular dependency.
// If you need to log, log directly to the console.

const connectionString = getMetaValue("ai-connection-string");

function createAppInsights() {
    const appInsights = new ApplicationInsights({
        config: {
            connectionString: connectionString,
            accountId: organizationId,
            excludeRequestFromAutoTrackingPatterns: [
                /\/browserLink/i,
                /:\/\/[^/]+\.fontawesome\.[^/]+\//i,
                /:\/\/[^/]+\.segment\.[^/]+\//i,
            ],
        },
    });

    appInsights.loadAppInsights();

    appInsights.addTelemetryInitializer((item) => {
        if(item?.tags) {
            item.tags["ai.cloud.role"] = "[Serious.Abbot]Abbot.Web.Client";
        }
    });

    appInsights.trackPageView();

    // This should run on every turbo load though.
    document.addEventListener('turbo:render', () => {
        appInsights.trackPageView();
    });
    console.log("App Insights initialized");
    return appInsights;
}

export const appInsights = connectionString ? createAppInsights() : null;
