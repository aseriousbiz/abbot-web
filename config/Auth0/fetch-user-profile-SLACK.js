/*
This is "Fetch User Profile Script" we use in Auth0 under:

Tenant -> Authentication -> Social -> `slack`
 */

function(accessToken, ctx, cb) {
    request.get({
        url: 'https://slack.com/api/openid.connect.userInfo',
        headers: {
            Authorization: 'Bearer ' + accessToken
        }
    }, function(err, resp, body) {
        if (err) return cb(err);
        if (resp.statusCode !== 200) return cb(new Error(body));

        const slackResponse = JSON.parse(body);
        if (!slackResponse.ok) return cb(new Error(body));

        let enterprise = null;
        const enterpriseId = slackResponse["https://slack.com/enterprise_id"];
        if (enterpriseId) {
            enterprise = {
                id: enterpriseId,
                name: slackResponse["https://slack.com/enterprise_name"],
                domain: slackResponse["https://slack.com/enterprise_domain"],
            };
        }

        const profile = {
            name: slackResponse.name,
            user_id: slackResponse["https://slack.com/user_id"],
            email: slackResponse.email,
            platform_user_id: slackResponse["https://slack.com/user_id"],
            slack_id: slackResponse["https://slack.com/user_id"],
            team: {
                id: slackResponse["https://slack.com/team_id"],
                name: slackResponse["https://slack.com/team_name"],
                domain: slackResponse["https://slack.com/team_domain"],
                image_68: slackResponse["https://slack.com/team_image_68"],
            },
            enterprise,
            picture: slackResponse["https://slack.com/user_image_32"],
        };
        cb(null, profile);
    });
}
