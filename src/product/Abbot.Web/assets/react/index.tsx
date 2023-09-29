import { createRoot } from 'react-dom/client';
import ErrorBoundary from './components/errorBoundary';
import { library } from '@fortawesome/fontawesome-svg-core'
import {
    faAt,
    faGlobe,
    faMessageBot,
    faMessagePlus,
    faPaperPlaneTop,
    faTimer,
    faUserPlus,
    faCircleQuestion,
    faFilter,
    faCodeBranch
} from '@fortawesome/pro-regular-svg-icons'
import PlaybookDefinitionLoader from "./models/playbookDefinitionLoader";
import { PlaybookContextProvider } from "./hooks/usePlaybooks";
import { PlaybookActivityContextProvider } from "./hooks/usePlaybookActivity";
import { ActivePanelContextProvider } from "./hooks/useActivePanel";
import PlaybookBuilder from "./components/playbookBuilder";
import { DebugContextProvider } from "./hooks/useDebug";
import { OverlayProvider } from "./hooks/useOverlay";
import 'react-tooltip/dist/react-tooltip.css'
import { OrganizationDataContextProvider } from './hooks/useOrganizationData';
import { ReadonlyProvider } from "./hooks/useReadonly";
import { AntiForgeryContextProvider } from './hooks/useAntiForgery';
import { FeatureFlagContextProvider } from './hooks/useFeatureFlags';

// There's a way to import these dynamically using babel-macro-plugins,
// (see: https://fontawesome.com/v6/docs/web/use-with/react/add-icons#add-icons-globally)
// but I ran into all sorts of WebPack errors trying to get it to work.
// So, for now, we'll just import the ones we use manually.

library.add(
    faGlobe,
    faMessageBot,
    faUserPlus,
    faMessagePlus,
    faPaperPlaneTop,
    faAt,
    faTimer,
    faCircleQuestion,
    faFilter,
    faCodeBranch);

/* I like having interactions with the page DOM happen here and not in the React app code as much as feasible */

const appElement = document.getElementById('react-root');
const root = createRoot(appElement);

const playbookDefinitionSource = document.getElementById('playbook-definition') as HTMLInputElement;
const webhookTriggerUrlSource = document.getElementById('webhook-trigger-url') as HTMLScriptElement;
const playbookDefinitionLoader = new PlaybookDefinitionLoader(playbookDefinitionSource, webhookTriggerUrlSource);

const antiforgeryToken = appElement.dataset.antiforgeryToken;
const readOnly = appElement.dataset.readonly && appElement.dataset.readonly.toLowerCase() === 'true';
const activeFeatureFlags = appElement.dataset.featureFlags?.split(',') ?? [];

root.render(
    <FeatureFlagContextProvider activeFeatureFlags={activeFeatureFlags}>
        <AntiForgeryContextProvider verificationToken={antiforgeryToken}>
            <ReadonlyProvider readonly={readOnly}>
                <DebugContextProvider>
                    <ErrorBoundary>
                        <OverlayProvider>
                            <PlaybookContextProvider playbookDefinitionLoader={playbookDefinitionLoader}>
                                <ActivePanelContextProvider>
                                    <PlaybookActivityContextProvider>
                                        <OrganizationDataContextProvider>
                                            <PlaybookBuilder />
                                        </OrganizationDataContextProvider>
                                    </PlaybookActivityContextProvider>
                                </ActivePanelContextProvider>
                            </PlaybookContextProvider>
                        </OverlayProvider>
                    </ErrorBoundary>
                </DebugContextProvider>
            </ReadonlyProvider>
        </AntiForgeryContextProvider>
    </FeatureFlagContextProvider>
);