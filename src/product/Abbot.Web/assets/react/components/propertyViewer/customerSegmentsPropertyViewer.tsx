import * as React from "react";
import {PropertyViewerProps} from "./index";
import {Option} from "../../models/options";

export function CustomerSegmentsPropertyViewer({value}: PropertyViewerProps<Option[]>) {
    function formatSegment(item: Option, index: number) {
        if (item.value) {
            const option = item as Option;
            return (
                <span key={option.value}
                      className="bg-gray-100 mr-1 px-1 rounded has-tooltip-arrow"
                      data-tooltip-id="global-tooltip"
                      data-tooltip-content={option.value}>
                    {option.label}
                    {index < (value as Option[]).length - 1 && ', '}
                </span>
            );
        }
    }

    if (!value || value.length === 0) {
        return (
            <>All customers</>
        );
    }
    return (
        <span>
            {value.map((option, index) => formatSegment(option, index))}
        </span>
    );
}