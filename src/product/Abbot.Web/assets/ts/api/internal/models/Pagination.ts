/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

export type Pagination = {
    /**
     * The current page number of the list.
     */
    pageNumber?: number;
    /**
     * The total number of pages.
     */
    totalPages?: number;
    /**
     * The maximum number of items per page.
     * Every page except the last page will have exactly this many items.
     * The last page will have no more than this many items, but may have fewer items.
     */
    pageSize?: number;
    /**
     * Whether or not there is a previous page.
     */
    hasPreviousPage?: boolean;
    /**
     * Whether or not there is a next page.
     */
    hasNextPage?: boolean;
};

