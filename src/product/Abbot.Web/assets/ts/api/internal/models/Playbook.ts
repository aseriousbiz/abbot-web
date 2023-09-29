/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */

import type { PlaybookModel } from './PlaybookModel';
import type { PlaybookVersionModel } from './PlaybookVersionModel';

export type Playbook = {
    playbook?: PlaybookModel;
    latestVersion?: PlaybookVersionModel;
};

