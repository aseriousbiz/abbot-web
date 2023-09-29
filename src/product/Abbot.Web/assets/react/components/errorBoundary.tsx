import * as React from "react";
import { appInsights } from "../../ts/telemetry";
import { SeverityLevel } from "@microsoft/applicationinsights-web";
import logger from "../../ts/log";
import { isDevelopment, isStaffMode } from "../../ts/env";

const log = logger("ErrorBoundary");

interface ErrorBoundaryProps {
    children: React.ReactNode,
}

interface ErrorBoundaryState {
    error?: Error
}

// Based on https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary
// Error Boundaries require handlers like componentDidCatch/getDerivedStateFromError and thus require a class component.
export default class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
    state: ErrorBoundaryState = {};
    constructor(props: ErrorBoundaryProps) {
        super(props);
    }

    static getDerivedStateFromError(error: Error) {
        // Update state so the next render will show the fallback UI.
        return { error };
    }

    componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
        log.error(error.message, error, errorInfo);
        appInsights?.trackException({
            exception: error,
            severityLevel: SeverityLevel.Error,
        }, {
            ...errorInfo,
        });
    }

    render() {
        if (this.state.error) {
            // You can render any custom fallback UI
            return <div className="w-full flex flex-col items-center p-5">
                <header className="text-red-600 px-2">
                    <p id="dialogTitle" className="font-semibold">Fatal Error</p>
                </header>
                <section className="p-2 text-center">
                    {(isStaffMode || isDevelopment) && (
                        <div className="mb-2">
                            <code>{this.state.error.message}</code>
                            <pre className="text-left">{this.state.error.stack}</pre>
                        </div>
                    )}
                    <p className="text-sm">
                        Try <a onClick={() => window.location.reload()}>reloading the page</a>
                        {' '}or contact <code>support@ab.bot</code> for help
                        {appInsights && (<>{' '}and provide this request ID <code>{appInsights.context.telemetryTrace.traceID}</code></>)}.
                    </p>
                </section>
            </div>
        }

        return this.props.children;
    }
}