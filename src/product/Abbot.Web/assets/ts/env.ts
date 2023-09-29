// Read the environment
// We don't refresh this when the page is updated but we assume it doesn't change.
const envMeta = document.head.querySelector<HTMLMetaElement>("meta[name='abbot-env']")

export const environment = envMeta ? envMeta.content.toLowerCase() : "production";
export const isDevelopment = environment === "development";
export const isProduction = environment === "production";
export const isLab = environment === "lab";
export const isCanary = environment === "canary";

/** Indicates if the user is staff.
 * 
 * NOTE: This is a client-side check and can be falsified!
 * Data that is actualy sensitive should be protected by server-side checks.
 */
export const isStaffMember = getMetaValue("abbot-member-staff") === "true";

/** Indicates if staff-mode is active
 * 
 * NOTE: This is a client-side check and can be falsified!
 * Data that is actualy sensitive should be protected by server-side checks.
 */
export const isStaffMode = getMetaValue("abbot-staff-mode") === "true";

export function getMetaValue(name: string) {
    const element = document.querySelector<HTMLMetaElement>(`meta[name="${name}"]`);
    if (element) {
        return element.content;
    }
}

export const memberId = getMetaValue("abbot-member-id");
export const organizationId = getMetaValue("abbot-organization-id");
