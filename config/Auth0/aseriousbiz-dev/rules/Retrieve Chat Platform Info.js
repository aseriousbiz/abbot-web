function(user, context, callback) {
  const namespace = 'https://schemas.ab.bot/';
  context.idToken[namespace + 'platform_user_id'] = user.platform_user_id;
  context.idToken[namespace + 'version'] = '0.0.112';

  if (context.connection.startsWith('slack')) {
    let platformUserId = context.idToken[namespace + 'platform_user_id'];
    if (platformUserId === null || platformUserId === undefined || platformUserId === '') {
      // We must be using the new Slack Social Connector. We need to parse the user id.
      // The user id is in the format "oauth2|slack|T000000AAAA-U000AAAA0A"
      // That last part is the PlatformId-PlatformUserId
      platformUserId = user.user_id.split('|')[2].split('-')[1];
      context.idToken[namespace + 'platform_user_id'] = platformUserId;
    }
    context.idToken[namespace + 'platform_id'] = user.team.id;
    context.idToken[namespace + 'platform_name'] = user.team.name;
    context.idToken[namespace + 'platform_domain'] = user.team.domain + '.slack.com';
    context.idToken[namespace + 'platform_avatar'] = user.team.image_68;
  }
  if (context.connection === 'azure-ad') {
    context.idToken[namespace + 'platform_user_id'] = user.oid;
    context.idToken[namespace + 'azure_tenant_id'] = user.tenantid;
  }
  callback(null, user, context);
}
