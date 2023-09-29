import logger from "./log";
import { AnalyticsBrowser } from '@segment/analytics-next'
import { getMetaValue, isStaffMember, memberId, organizationId } from "./env";

const log = logger("analytics");

const writeKey = getMetaValue("segment-write-key");
if (writeKey) {
    log.verbose("Configuring Segment");
    const analytics = AnalyticsBrowser.load({
        writeKey: writeKey
    });

    if (log.isEnabled()) {
        analytics.debug(true);
    }

    if (memberId && memberId.length > 0) {
        log.verbose(`Marking current member: ${memberId}`);
        analytics.identify(memberId, {
            name: getMetaValue("abbot-member-name"),
            is_staff: isStaffMember,
        });
    }

    if (organizationId && organizationId.length > 0) {
        log.verbose(`Marking current group: ${organizationId}`);
        analytics.group(organizationId, {
            organization: organizationId,
            plan: getMetaValue("abbot-organization-plan"),
            is_serious: getMetaValue("abbot-organization-is-serious") === "true",
            created: getMetaValue("abbot-organization-created"),
        });
    }

    // This should run on every turbo load though.
    document.addEventListener('turbo:render', () => {
        // Read values from the head meta tags
        const category = document.head.querySelector<HTMLMetaElement>('meta[name="abbot-page-category"]')?.content;
        const name = document.head.querySelector<HTMLMetaElement>('meta[name="abbot-page-name"]')?.content;
        const title = document.head.querySelector<HTMLMetaElement>('meta[name="abbot-page-title"]')?.content;
        const endpointName = document.head.querySelector<HTMLMetaElement>('meta[name="abbot-endpoint-name"]')?.content;
        log.verbose(`Emitting page view to ${category} / ${name} - ${title} - ${endpointName}`);
        analytics.page(category, name, { endpoint_name: endpointName });
    });
}
