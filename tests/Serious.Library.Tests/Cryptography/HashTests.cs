using Serious.Cryptography;
using Xunit;

public class HashTests
{
    public class TheComputeHMACSHA256HashMethod
    {
        [Fact]
        public void ComputesTheHash()
        {
            var result = "test".ComputeHMACSHA256Hash("secret");
            var secondResult = "tests".ComputeHMACSHA256Hash("ecret");
            Assert.Equal("Aymga2LNFrM+tnkr6MYLFY2Jou46h2/Omogeu0iMCRQ=", result);
            Assert.Equal("h0bUmhbY46eTMvGYVEr8WGPyLD3hBFcc4FMt3Lcng/A=", secondResult);
            Assert.NotEqual(result, secondResult);
        }
    }

    public class TheComputeHMACSHA256FileNameMethod
    {
        [Fact]
        public void ComputesFileNameFriendlyHash()
        {
            var result = "test".ComputeHMACSHA256FileName("secret");

            Assert.Equal("0329a06b62cd16b33eb6792be8c60b158d89a2ee3a876fce9a881ebb488c0914", result);
        }
    }
}
