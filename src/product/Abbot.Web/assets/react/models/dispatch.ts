import * as Api from "../../ts/api/internal";

export type DispatchSettings = Api.DispatchSettings;
export type DispatchType = Api.DispatchType;

export const allDispatchOptions: Record<DispatchType, string> = {
    'Once': "Run once",
    'ByCustomer': "Run once for each customer",
};