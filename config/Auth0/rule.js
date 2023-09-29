/*
This is "Retrieve Chat Platform Info" rule we use in Auth0 under:

Tenant -> Auth Pipeline -> Rules -> "Retrieve Chat Platform Info"
 */
function(user, context, callback) {
  const namespace = 'https://schemas.ab.bot/';

  context.idToken[namespace + 'platform_user_id'] = user.platform_user_id;
  context.idToken[namespace + 'version'] = '0.0.113';

  if (user.enterprise) {
    context.idToken[namespace + 'enterprise_id'] = user.enterprise.id;
    context.idToken[namespace + 'enterprise_name'] = user.enterprise.name;
    context.idToken[namespace + 'enterprise_domain'] = user.enterprise.domain;
  }

  context.idToken[namespace + 'platform_id'] = user.team.id;
  context.idToken[namespace + 'platform_name'] = user.team.name;
  context.idToken[namespace + 'platform_domain'] = user.team.domain + '.slack.com';
  context.idToken[namespace + 'platform_avatar'] = user.team.image_68;

  callback(null, user, context);
}
