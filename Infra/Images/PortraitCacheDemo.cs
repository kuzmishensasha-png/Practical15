using System.Net;
using System.Net.Http.Headers;

namespace Practical1.Infra.Images;

public static class PortraitCacheDemo
{
    private const string DemoId = "portrait-demo";
    private const string DemoUrl = "https://example.invalid/practical15/demo-portrait.png";

    public static async Task<string> RunAsync(string cacheDirectory)
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "demo-portrait.png");
        if (!File.Exists(samplePath))
        {
            throw new FileNotFoundException("Demo image was not found.", samplePath);
        }

        byte[] sample = await File.ReadAllBytesAsync(samplePath);
        using DemoImageHandler handler = new(sample);
        using HttpClient simulatedClient = new(handler);
        CharacterPortraitCache cache = new(simulatedClient, cacheDirectory);

        bool wasCached = cache.GetCachedPath(DemoId, DemoUrl) != null;
        string firstPath = await cache.GetOrDownloadAsync(DemoId, DemoUrl);

        CharacterPortraitCache restartedCache = new(simulatedClient, cacheDirectory);
        string secondPath = await restartedCache.GetOrDownloadAsync(DemoId, DemoUrl);

        bool reused = firstPath == secondPath;
        return $"Offline demonstration (simulated HTTP image):\n" +
            $"First access: {(wasCached ? "loaded from cache" : "saved to disk")}\n" +
            $"Second access after simulated restart: {(reused ? "same file" : "different file")}\n" +
            $"Simulated HTTP requests: {handler.RequestCount}\n" +
            $"Image: {firstPath}\n" +
            "This is only a cache test; it does not mean the external Genshin API is online.";
    }

    private sealed class DemoImageHandler : HttpMessageHandler
    {
        private readonly byte[] bytes;
        public int RequestCount { get; private set; }

        public DemoImageHandler(byte[] bytes)
        {
            this.bytes = bytes;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            ByteArrayContent content = new(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }
}
