import { PropsWithChildren, createContext, useContext } from "react";

export interface FeatureFlagsValue {
    activeFeatureFlags: string[],
    hasFeature: (feature: string) => boolean,
}

export interface FeatureFlagsProps {
    activeFeatureFlags: string[],
}

export const FeatureFlagContext = createContext({} as FeatureFlagsValue);

export default function useFeatureFlags() {
    const context = useContext(FeatureFlagContext);
    if (!context) throw new Error("FeatureFlagContext not found!");
    return context;
}

export function FeatureFlagContextProvider(props: PropsWithChildren<FeatureFlagsProps>) {
    const activeFeatureFlags = props.activeFeatureFlags;
    const value = {
        activeFeatureFlags,
        hasFeature: (feature: string) => activeFeatureFlags.includes(feature),
    }

    return (
        <FeatureFlagContext.Provider value={value}>
            {props.children}
        </FeatureFlagContext.Provider>
    );
}