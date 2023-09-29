using System.Text;
using Serious.Cryptography;
using Serious.Text;
using Xunit;

public class BaseEncoderTests
{
    public class TheToBase62Method
    {
        [Fact]
        public void EncodesUlongCorrectly()
        {
            // This is an example of a GitHub token (since revoked).
            var checksum = Crc32.Compute("fIkRGaBu5PeKsznMzXQOg3kETH1Sgx", Encoding.UTF8);

            var encoded = BaseEncoder.ToBase62(checksum);

            Assert.Equal("2oIfSC", encoded);
        }
    }
}
