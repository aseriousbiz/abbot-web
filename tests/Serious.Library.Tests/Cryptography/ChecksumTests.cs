using Serious.Cryptography;
using Xunit;

public class ChecksumTests
{
    public class TheComputeCrc32Base62ChecksumMethod
    {
        [Fact]
        public void ComputesChecksum()
        {
            var result = Checksum.ComputeCrc32Base62Checksum(
                "fIkRGaBu5PeKsznMzXQOg3kETH1Sgx",
                6,
                '0');
            Assert.Equal("2oIfSC", result);
        }
    }
}
