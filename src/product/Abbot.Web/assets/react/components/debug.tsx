import { PropsWithChildren } from "react";
import useDebug from "../hooks/useDebug";

export default function Debug({children}: PropsWithChildren) {
    const {debug} = useDebug();
    if (!debug) {
        return null;
    }
    return (
        <div>
            {children}
        </div>
    );
}