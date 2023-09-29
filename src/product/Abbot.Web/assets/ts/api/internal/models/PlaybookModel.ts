/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

export type PlaybookModel = {
    /**
     * The name of the playbook
     */
    name?: string | null;
    /**
     * The slug for the playbook.
     * This is the short name used in URLs.
     * It is case-insensitive and can only contain letters, numbers, and hyphens.
     */
    slug?: string | null;
    /**
     * A description of the playbook
     */
    description?: string | null;
};

