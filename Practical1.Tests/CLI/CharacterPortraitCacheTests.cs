using System.Net;
using System.Net.Http.Headers;
using Practical1.Infra.Images;

namespace Practical1.Tests.CLI;

public class CharacterPortraitCacheTests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lN8AAAAASUVORK5CYII=");

    [Fact]
    public async Task FirstRequestSavesImage_SecondRequestUsesDiskWithoutNetwork()
    {
        string directory = Path.Combine(Path.GetTempPath(), "portrait-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            int requests = 0;
            using HttpClient client = new(new FakeHandler(_ =>
            {
                requests++;
                return CreateImageResponse();
            }));

            CharacterPortraitCache cache = new(client, directory);
            string url = "https://example.com/characters/amber/portrait";
            string first = await cache.GetOrDownloadAsync("amber", url);
            Assert.True(File.Exists(first));
            Assert.Equal(PngBytes, await File.ReadAllBytesAsync(first));

            CharacterPortraitCache nextRun = new(client, directory);
            string second = await nextRun.GetOrDownloadAsync("amber", url);
            Assert.Equal(first, second);
            Assert.Equal(first, nextRun.GetCachedPath("amber", url));
            Assert.Equal(1, requests);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task HtmlResponseIsRejectedAndIsNotCached()
    {
        string directory = Path.Combine(Path.GetTempPath(), "portrait-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using HttpClient client = new(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Error</html>")
            }));
            CharacterPortraitCache cache = new(client, directory);
            string url = "https://example.com/characters/amber/portrait";

            await Assert.ThrowsAsync<InvalidDataException>(() => cache.GetOrDownloadAsync("amber", url));
            Assert.Null(cache.GetCachedPath("amber", url));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatedImageUrlUsesDifferentFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "portrait-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using HttpClient client = new(new FakeHandler(_ => CreateImageResponse()));
            CharacterPortraitCache cache = new(client, directory);
            string first = await cache.GetOrDownloadAsync("amber", "https://example.com/portrait?v=1");
            string second = await cache.GetOrDownloadAsync("amber", "https://example.com/portrait?v=2");

            Assert.NotEqual(first, second);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CorruptedCachedFile_IsDownloadedAgain()
    {
        string directory = Path.Combine(Path.GetTempPath(), "portrait-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            int requests = 0;
            using HttpClient client = new(new FakeHandler(_ =>
            {
                requests++;
                return CreateImageResponse();
            }));
            CharacterPortraitCache cache = new(client, directory);
            const string url = "https://example.com/portrait";
            string path = await cache.GetOrDownloadAsync("amber", url);
            Assert.Equal(1, requests);
            await File.WriteAllTextAsync(path, "invalid file content");

            Assert.Null(cache.GetCachedPath("amber", url));
            string repairedPath = await cache.GetOrDownloadAsync("amber", url);
            Assert.Equal(path, repairedPath);
            Assert.Equal(PngBytes, await File.ReadAllBytesAsync(repairedPath));
            Assert.Equal(2, requests);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpResponseMessage CreateImageResponse()
    {
        ByteArrayContent content = new(PngBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
