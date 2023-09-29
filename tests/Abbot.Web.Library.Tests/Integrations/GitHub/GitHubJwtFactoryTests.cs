using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Serious;
using Serious.Abbot.Integrations.GitHub;

namespace Abbot.Web.Library.Tests.Integrations.GitHub;

public class GitHubJwtFactoryTests
{
    public class TheGenerateJwtMethod
    {
        [Fact]
        public void GeneratesToken()
        {
            var clock = new TimeTravelClock();

            var factory = new GitHubJwtFactory(_gitHubOptions, clock);

            var now = clock.Freeze();
            var token = factory.GenerateJwt();
            AssertToken(now, token);

            // Make sure we can call it again
            var then = clock.AdvanceBy(TimeSpan.FromSeconds(5));
            token = factory.GenerateJwt();
            AssertToken(then, token);

            static void AssertToken(DateTime now, string token)
            {
                var jwt = Assert.IsType<JwtSecurityToken>(new JwtSecurityTokenHandler().ReadToken(token));
                Assert.Equal("RS256", jwt.Header.Alg);
                Assert.Equal("287745", jwt.Issuer);
                Assert.Equal(new[] { "https://api.github.com" }, jwt.Audiences);
                Assert.Equal(now.AddMinutes(5), jwt.ValidTo, TimeSpan.FromSeconds(1));
                Assert.Equal(now.AddMinutes(-1), jwt.IssuedAt, TimeSpan.FromSeconds(1));
            }
        }
    }

    static IOptions<GitHubOptions> _gitHubOptions = Options.Create(new GitHubOptions
    {
        AppId = "287745",
        AppKey =
            // Real key from deleted app
            """
            -----BEGIN RSA PRIVATE KEY-----
            MIIEowIBAAKCAQEAzO+Hk1OgouBo5TV3JezxTlValBfRmdfV03L3baXBpRo1+Qsy
            wmRg54QoJeCaWFutxIOvGSkMjRbwAqs8v1hloPYEsOKNRJe0BU/OE58h8+mGHLT2
            dz04G6n0q61EiNauq2EFN9qyJCG439RErLyQAfHqAGMTRM9TWUTKoik8ZcQCr5BE
            rlYeznVhWz4bLmP2Qe1Dctay33eUbhNv54l56xvFe81CW7GzWOJflIxNQJ0fSA+c
            dxOsubqiMBWV350It9KZncPq4/X9fZ+G9Xr6+evvE0aXBWnL/5S66F/rE9BfKey+
            RK9HVk+9I7MRnjM8n8g2kAK/bxN0YrtRxdDbEQIDAQABAoIBAEW0VyXKNQIRWDxV
            8h/JNs5RA80JSPaNziHsobH+xh21C5SYtXwfDkLQ1aMEgRr6m+ESdTUWnDlFCv+t
            ZK0kkPStmSzc8fXZr5Z67XoJD1BaJo6PEqG+Bd6K8TiPZ5cvhhuulUrJLPxTKAGh
            vnYOcODoepIFIOGvrwbW+iEr62olJDlBUJq2uzr8JOTJT4M8y6qlMIwpohdMlBSJ
            h7QETprMOsR9DYZFOSinCbmZLfcqH06ZKKHKLwOqs7iqkuhrpmayisxp7EXh1K/S
            AmMwGHPtIyDlhWrkP2R7JwMrOcA1LUMg4/cpaAFx5pmNE9BMQwOidsDxtE5r/xZ9
            oBKqoO0CgYEA8x6x+SJWw31+hfDCLHyEJfnX8xyhHq3NZ4uK0s5LBSqGzEmSdz17
            +DqLHygKf6SPlJuCAehIvXYxwpXBOlKGtaWGI/J/R85Sg68mywGqNsE4QfCJNzBd
            xhqb5tLzz36F2RrZ2hL/gAfmdabcFF19lnq91RQgNFRFZ19UFpUxTGcCgYEA18r2
            RrkaxU8/sWI2hFS7Vw7ejcBnayNKPcb2xRHZDXubsmLkMGJcjhEBTyO+HPC0LPyv
            ss1T45X5LOxn0TKs14GYXb9VrA5eIzqugIShAbtjYFmplUDPOJcFTm91K37iwZc+
            RGA0tY2Tqvjo+zFXrfpwcE5Afq1U4kwIw9zzcccCgYBz0q3LGNbo70J1oQuAkhmK
            1gpRYdRIf1iZ+dq7L1iCL45kiLBkakBDM/DPeQ33XXihvawkKHtu934hS6LwnBxd
            MWxEd+S3Ws3oumrqz/I9f7PDhkp4pmwmUsrvHpTUx1wQ4D/lKqPaZOkgJ8w1T4zj
            QbpqZtoo0/T0mG/BCnagBQKBgQCxQi1vBtpwvZpqUWzK4vdImhRCiIvrO//eIzPN
            yc1r/99zdzxOal3w7RMQOSIPj8HROnfw/i0sw6L9PexBsci89d19FJCBVwQJGEkD
            lO7VB2KoYL6mtagCqjtXpMKwyffmYiBp9kUV5YgpZ3Gp9Ww6o3/9IKpl5GfXw/Fn
            QnZPPQKBgCJ+v9MVW0JBqPIQpZxNismSqYbXbnWxKj/uEWp/fSyZdBEhx8Co1Dtw
            RqOTUo02t0aCxltWyf5J/Ft6f5tAoZQQ0ZoeLCNKFk0V17M8Z+TCtu/nZja0yhUC
            kxeODNGvm55fIwRPataJh4xEhQLfzu726kjMsKNXj+YO1eJUsOuy
            -----END RSA PRIVATE KEY-----
            """
    });
}
