using Serious.Abbot.Serialization;
using Serious.Slack.Abstractions;
using Serious.Slack.Events;

public class SharedChannelInviteAcceptedTests
{
    [Fact]
    public void CanBeDeserialized()
    {
        var payload =
          """
          {
            "token": "REDACTED",
            "team_id": "T013108BYLS",
            "api_app_id": "A01TG9GPJQ3",
            "event": {
              "type": "shared_channel_invite_accepted",
              "approval_required": false,
              "invite": {
                "id": "I05M5GCNVNW",
                "date_created": 1691636185,
                "date_invalid": 1692845785,
                "inviting_team": {
                  "id": "T013108BYLS",
                  "name": "Abbot",
                  "icon": {
                    "image_default": false,
                    "image_34": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_34.png",
                    "image_44": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_44.png",
                    "image_68": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_68.png",
                    "image_88": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_88.png",
                    "image_102": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_102.png",
                    "image_230": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_230.png",
                    "image_132": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_132.png"
                  },
                  "avatar_base_url": "https://ca.slack-edge.com/",
                  "is_verified": false,
                  "domain": "aseriousbiz",
                  "date_created": 1588619349
                },
                "inviting_user": {
                  "id": "U01TG976JSW",
                  "team_id": "T013108BYLS",
                  "name": "abbot-haacked-dev",
                  "updated": 1677707508,
                  "who_can_share_contact_card": "EVERYONE",
                  "profile": {
                    "real_name": "abbot-haacked-dev",
                    "display_name": "",
                    "real_name_normalized": "abbot-haacked-dev",
                    "display_name_normalized": "",
                    "team": "T013108BYLS",
                    "avatar_hash": "62d768089764",
                    "email": "botuser-T013108BYLS-B01U63BS0EL@slack-bots.com",
                    "image_24": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_24.png",
                    "image_32": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_32.png",
                    "image_48": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_48.png",
                    "image_72": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_72.png",
                    "image_192": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_192.png",
                    "image_512": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_512.png",
                    "image_1024": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_1024.png",
                    "image_original": "https://avatars.slack-edge.com/2022-06-10/3649778421509_62d76808976498a0738c_original.png",
                    "is_custom_image": true
                  }
                },
                "recipient_email": "haacked@gmail.com"
              },
              "channel": {
                "id": "C05M7V86LNQ",
                "is_im": false,
                "is_private": false,
                "name": "haacked-dev-team-haacked-test-emporium"
              },
              "teams_in_channel": [
                {
                  "id": "T013108BYLS",
                  "name": "Abbot",
                  "icon": {
                    "image_default": false,
                    "image_34": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_34.png",
                    "image_44": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_44.png",
                    "image_68": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_68.png",
                    "image_88": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_88.png",
                    "image_102": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_102.png",
                    "image_230": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_230.png",
                    "image_132": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_132.png"
                  },
                  "avatar_base_url": "https://ca.slack-edge.com/",
                  "is_verified": false,
                  "domain": "aseriousbiz",
                  "date_created": 1588619349
                },
                {
                  "id": "TFWSDE3AN",
                  "name": "Haacked",
                  "icon": {
                    "image_default": false,
                    "image_34": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_34.png",
                    "image_44": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_44.png",
                    "image_68": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_68.png",
                    "image_88": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_88.png",
                    "image_102": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_102.png",
                    "image_230": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_230.png",
                    "image_132": "https://avatars.slack-edge.com/2019-02-04/540920717664_97bf2b8ffe9b5e4406b6_132.png"
                  },
                  "avatar_base_url": "https://ca.slack-edge.com/",
                  "is_verified": false,
                  "domain": "haacked",
                  "date_created": 1549220119
                }
              ],
              "accepting_user": {
                "id": "UFW4S427J",
                "team_id": "TFWSDE3AN",
                "name": "haacked",
                "updated": 1603971740,
                "who_can_share_contact_card": "EVERYONE",
                "profile": {
                  "real_name": "Phil Haack",
                  "display_name": "",
                  "real_name_normalized": "Phil Haack",
                  "display_name_normalized": "",
                  "team": "TFWSDE3AN",
                  "avatar_hash": "gdf546b601bf",
                  "email": "haacked@gmail.com",
                  "image_24": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d24\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-24.png",
                  "image_32": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d32\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-32.png",
                  "image_48": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d48\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-48.png",
                  "image_72": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d72\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-72.png",
                  "image_192": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d192\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-192.png",
                  "image_512": "https://secure.gravatar.com/avatar/cdf546b601bf29a7eb4ca777544d11cd.jpg?s\u003d512\u0026d\u003dhttps%3A%2F%2Fa.slack-edge.com%2Fdf10d%2Fimg%2Favatars%2Fava_0016-512.png"
                }
              },
              "event_ts": "1691636205.000000"
            },
            "type": "event_callback",
            "event_id": "Ev05M2JMUV37",
            "event_time": 1691636205,
            "authorizations": [
              {
                "team_id": "T013108BYLS",
                "user_id": "U01TG976JSW",
                "is_bot": true,
                "is_enterprise_install": false
              }
            ],
            "is_ext_shared_channel": false
          }
          """;

        var envelope = AbbotJsonFormat.NewtonsoftJson.Deserialize<IElement>(payload);

        var result = Assert.IsAssignableFrom<IEventEnvelope<SharedChannelInviteAccepted>>(envelope);
        var eventBody = result.Event;
        Assert.Equal("I05M5GCNVNW", eventBody.Invite.Id);
        Assert.Equal("C05M7V86LNQ", eventBody.Channel.Id);
        Assert.NotNull(eventBody.AcceptingUser);
        Assert.Equal("UFW4S427J", eventBody.AcceptingUser.Id);
        Assert.Equal("TFWSDE3AN", eventBody.AcceptingUser.TeamId);
    }
}
