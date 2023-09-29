import { PropsWithChildren, createContext, useContext } from "react";

export interface AntiForgeryValue {
    verificationToken?: string,
}

export interface AntiForgeryProps {
    verificationToken: string,
}

export const AntiForgeryContext = createContext({} as AntiForgeryValue);

export default function useAntiForgery() {
    const context = useContext(AntiForgeryContext);
    if (!context) throw new Error("AntiForgeryContext not found!");
    return context;
}

export function AntiForgeryContextProvider(props: PropsWithChildren<AntiForgeryProps>) {
    // We don't expect the verification token to change for the entire app lifetime, so we don't need it in state.
    // But we _might_ need to consider a way to refresh it, so it's still good for consumers to access it from the context rather than a global.
    const value = {
        verificationToken: props.verificationToken
    };

    return (
        <AntiForgeryContext.Provider value={value}>
            {props.children}
        </AntiForgeryContext.Provider>
    );
}