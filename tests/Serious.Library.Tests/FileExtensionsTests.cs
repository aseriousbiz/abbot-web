using Serious.Abbot.Extensions;
using Xunit;

public class FileExtensionsTests
{
    public class TheParseFileTypeMethod
    {
        [Theory]
        [InlineData("bmp", new byte[] { 0x42, 0x4d, 0x00, 0x00, 0x00 })]
        [InlineData("gif", new byte[] { 0x47, 0x49, 0x46, 0x00, 0x00, 0x00 })] // Prounounced Jif. For "Gif" the bytes are 0xWRONG
        [InlineData("png", new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x00, 0x00, 0x00 })]
        [InlineData("tiff", new byte[] { 0x4d, 0x4d, 0x00, 0x2a, 0x00, 0x00, 0x00 })]
        [InlineData("tiff", new byte[] { 0x49, 0x49, 0x2a, 0x00, 0x00, 0x00 })]
        [InlineData("jpeg", new byte[] { 0xff, 0xd8, 0x00, 0x00, 0x00 })]
        [InlineData("pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x00, 0x00, 0x00 })]
        public void ParsesBytesOfFileTypes(string expectedType, byte[] bytes)
        {
            var fileType = bytes.ParseFileType();

            Assert.Equal(expectedType, fileType);
        }

        [Fact]
        public void ReturnsUnknownWhenSequenceTooShort()
        {
            var bytes = new byte[] { 0x47, 0x49 };

            var fileType = bytes.ParseFileType();

            Assert.Equal("unknown", fileType);
        }
    }

    public class TheIsBase64EncodedFileMethod
    {
        [Fact]
        public void ReturnsTrueWhenForKnownRealPngFile()
        {
            var image = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

            var result = image.IsBase64EncodedFile();

            Assert.True(result);
        }

        [Theory]
        [InlineData("Qk0AAAAGgAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("R0lGAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("iVBORw0KGgoAAAANSUhEUgAAACAAAAAg")]
        [InlineData("TU0AKgAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("SUkqAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("/9gAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        [InlineData("JVBERgAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        public void ReturnsTrueForFabricatedFiles(string encodedFile)
        {
            var result = encodedFile.IsBase64EncodedFile();

            Assert.True(result);
        }

        [Theory]
        [InlineData("http://example.com/image")]
        [InlineData("https://example.com/image")]
        [InlineData("file://example.com/image")]
        [InlineData("ftp://example.com/image")]
        [InlineData("AAAMJlWElmTU0AKgAAAAgABwESAAMAAAABA")]
        public void ReturnsFalseWhenFileReferenceIsUrlOrUnknown(string fileReference)
        {
            var result = fileReference.IsBase64EncodedFile();
            Assert.False(result);
        }
    }
}
