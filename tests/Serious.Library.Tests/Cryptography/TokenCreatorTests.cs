using System;
using System.Collections.Generic;
using System.Linq;
using Serious;
using Serious.Cryptography;
using Xunit;

public class TokenCreatorTests
{
    public class TheCreateStrongAuthenticationTokenMethod
    {
        [Theory]
        [InlineData("abk")]
        [InlineData("abk_")]
        public void CreatesStrongAuthenticationToken(string prefix)
        {
            var token = TokenCreator.CreateStrongAuthenticationToken(prefix);

            Assert.StartsWith("abk_", token);
            Assert.True(TokenCreator.IsValidTokenFormat(token, TokenCreator.DefaultApiTokenLength, "abk"));
        }

        [Fact]
        public void AlphabetHasNoDuplicates()
        {
            var seen = new HashSet<char>();
            foreach (var chr in TokenCreator.TokenAlphabet)
            {
                Assert.True(seen.Add(chr), $"Character {chr} is duplicated in the token alphabet");
            }
        }

        [Fact]
        public void IsUnbiased()
        {
            const int sampleSize = 100_000;
            // Generate a lot of tokens
            var tokens = Enumerable.Range(0, sampleSize).Select(i => TokenCreator.CreateStrongAuthenticationToken("abk")).ToArray();

            // Compute the frequency of each character
            var characterCounts = new int[TokenCreator.TokenAlphabet.Length];
            foreach (var token in tokens)
            {
                // Ignore the prefix and checksum
                var cleanedToken = token[("abk_".Length)..^6];
                foreach (var chr in cleanedToken)
                {
                    var idx = TokenCreator.TokenAlphabet.IndexOf(chr);
                    Assert.InRange(idx, 0, TokenCreator.TokenAlphabet.Length - 1);
                    characterCounts[idx]++;
                }
            }

            // Every character should be equally likely.
            double expected = (sampleSize * TokenCreator.DefaultApiTokenLength) / (double)TokenCreator.TokenAlphabet.Length;

            // But randomness is random, so we have a tolerance of about 5% either way
            double tolerance = 0.05 * expected;

            for (var i = 0; i < characterCounts.Length; i++)
            {
                int occurrences = characterCounts[i];

                // How far was it from the expected likelihood?
                double actual = Math.Abs(expected - occurrences);

                // Is it within tolerance?
                Assert.True(actual < tolerance, $"Character {TokenCreator.TokenAlphabet[i]} occurred {occurrences} number of times in the sample, which is {actual} more times than it would in a perfect random distribution");
            }
        }
    }

    public class TheIsValidTokenFormatMethod
    {
        [Theory]
        [InlineData("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", "ghp", 30, true)]
        [InlineData("abk_fIkRGaBu5PeKsznMzXQOg3kETH1Sgx2oIfSC", "abk", 30, true)]
        [InlineData("abk_2735fjMHPTctPcM4jTm42sSJ1j5Xlp", "abk", 24, true)]
        [InlineData("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", "ghc", 30, false)]
        [InlineData("abk_fIkRGaBu5PeKsznMzXQOg3kETH1Sgx2oIfSC", "abk", 29, false)]
        [InlineData("abk_2735fjMHPTctPcM4jTm42sSJ1j5Xlq", "abk", 24, false)]
        public void IsTrueForValidTokensFalseOtherwise(string token, string prefix, int randomPartLength, bool expected)
        {
            var result = TokenCreator.IsValidTokenFormat(token, randomPartLength, prefix);

            Assert.Equal(expected, result);
        }
    }
}
