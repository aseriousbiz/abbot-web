using System;
using Serious.Slack;
using Xunit;

public class VerificationTests
{
    public class TheVerifyRequestMethod
    {
        [Theory]
        [InlineData(6, VerificationResult.TimestampExpired)]
        [InlineData(5, VerificationResult.Ok)]
        public void ReturnsConflictWhenTimeStampOutOfRange(int minutesOffset, VerificationResult expected)
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1616207883)
                .AddMinutes(minutesOffset);

            var result = Verification.VerifyRequest("{\"payload\"{}}",
                "1616207883",
                "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BC",
                "signingSecret",
                now,
                null);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ReturnsBadRequestWhenSignatureDoesNotMatch()
        {
            var now = DateTimeOffset.FromUnixTimeSeconds(1616207883);

            var result = Verification.VerifyRequest("{\"payload\"{}}",
                "1616207883",
                "v0=F5888A3019DFB7F67C9EFB481BAC08D00B737054912905E9CBF63D0B119589BD",
                "signingSecret",
                now,
                null);

            Assert.Equal(VerificationResult.SignatureMismatch, result);
        }
    }
}
