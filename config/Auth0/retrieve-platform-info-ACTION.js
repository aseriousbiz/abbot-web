exports.onExecutePostLogin = async (event, api) => {
    const namespace = 'https://schemas.ab.bot/';

    api.idToken.setCustomClaim(namespace + 'platform_user_id', event.user.platform_user_id);
    api.idToken.setCustomClaim(namespace + 'version', '0.0.113');

    if (event.user.enterprise) {
        api.idToken.setCustomClaim(namespace + 'enterprise_id', event.user.enterprise.id);
        api.idToken.setCustomClaim(namespace + 'enterprise_name', event.user.enterprise.name);
        api.idToken.setCustomClaim(namespace + 'enterprise_domain', event.user.enterprise.domain);
    }

    api.idToken.setCustomClaim(namespace + 'platform_id', event.user.team.id);
    api.idToken.setCustomClaim(namespace + 'platform_name', event.user.team.name);
    api.idToken.setCustomClaim(namespace + 'platform_domain', event.user.team.domain + '.slack.com');
    api.idToken.setCustomClaim(namespace + 'platform_avatar', event.user.team.image_68);
};