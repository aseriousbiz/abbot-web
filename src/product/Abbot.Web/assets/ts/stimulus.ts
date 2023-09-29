export type StimulusEvent<T = unknown> = CustomEvent & {
    params: T
};
