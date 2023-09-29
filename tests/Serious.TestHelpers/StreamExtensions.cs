using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Serious.TestHelpers
{
    public static class StreamExtensions
    {
        public static async Task WriteStringAsync(this Stream stream, string text)
        {
            var streamWriter = new StreamWriter(stream);
            await streamWriter.WriteAsync(text);
            await streamWriter.FlushAsync();
            stream.Position = 0;
        }

        public static Task<string> ReadAsStringAsync(this Stream stream)
        {
            stream.Position = 0;
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEndAsync();
        }

        public static async Task<T> ReadAsAsync<T>(this Stream stream)
        {
            var json = await stream.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
