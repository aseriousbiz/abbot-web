import {Key} from "react";

export type PropsWithKey<P> = P & { key?: Key };

export type ItemWithIndex<TItem> = {item?: TItem, index?: number };
