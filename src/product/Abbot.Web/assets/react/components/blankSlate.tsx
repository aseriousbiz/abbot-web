import {PropsWithChildren} from "react";

export interface BlankSlateProps {
    title: string,
}

export default function BlankSlate({ title, children } : PropsWithChildren<BlankSlateProps>) {
    return (
        <div className="text-center p-8">
            <h2 className="font-semibold text-xl">
                {title}
            </h2>
            <div>
                {children}
            </div>
        </div>
    )
}