using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Practical1.Infra.Images;

public sealed class CharacterPortraitCache
{
    private const int MaxImageSize = 10 * 1024 * 1024;
    private readonly HttpClient httpClient;

    public string CacheDirectory { get; }

    public CharacterPortraitCache(HttpClient httpClient, string? cacheDirectory = null)
    {
        this.httpClient = httpClient;
        string baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".practical1");
        }

        CacheDirectory = cacheDirectory ?? Path.Combine(baseDirectory, "Practical1", "Portraits");
    }

    public string? GetCachedPath(string id, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(imageUrl)
            || !Directory.Exists(CacheDirectory))
        {
            return null;
        }

        string prefix = FilePrefix(id, imageUrl);
        foreach (string extension in new[] { ".png", ".jpg", ".gif", ".webp" })
        {
            string path = Path.Combine(CacheDirectory, prefix + extension);
            if (File.Exists(path) && HasValidSignature(path, extension))
            {
                return path;
            }
        }

        return null;
    }

    public async Task<string> GetOrDownloadAsync(
        string id, string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Character ID cannot be empty.", nameof(id));
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? imageUri)
            || (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Image must have a full HTTP(S) URL.", nameof(imageUrl));
        }

        string? cachedPath = GetCachedPath(id, imageUrl);
        if (cachedPath != null)
        {
            return cachedPath;
        }

        using HttpResponseMessage response = await httpClient.GetAsync(
            imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        string extension = ImageExtension(response.Content.Headers.ContentType);
        if (response.Content.Headers.ContentLength > MaxImageSize)
        {
            throw new InvalidDataException("Portrait is too large (maximum 10 MB).");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaxImageSize)
            {
                throw new InvalidDataException("Portrait is too large (maximum 10 MB).");
            }
            buffer.Write(chunk, 0, count);
        }

        byte[] image = buffer.ToArray();
        if (!ValidImageSignature(image, extension))
        {
            throw new InvalidDataException("Server did not return a valid portrait image.");
        }

        Directory.CreateDirectory(CacheDirectory);
        string path = Path.Combine(CacheDirectory, FilePrefix(id, imageUrl) + extension);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, image, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return path;
    }

    private static string FilePrefix(string id, string imageUrl)
    {
        string safeId = new string(id.Trim().ToLowerInvariant()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_')
            .Take(40).ToArray());
        string hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(id.Trim().ToLowerInvariant() + "|" + imageUrl.Trim())))[..12];
        return safeId + "-" + hash;
    }

    private static string ImageExtension(MediaTypeHeaderValue? contentType)
    {
        return contentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => throw new InvalidDataException("Server response is not a supported image (PNG, JPEG, GIF or WebP).")
        };
    }

    private static bool HasValidSignature(string path, string extension)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length < 12) return false;
            Span<byte> header = stackalloc byte[12];
            if (stream.Read(header) != header.Length) return false;
            if (extension == ".jpg")
            {
                stream.Seek(-2, SeekOrigin.End);
                Span<byte> end = stackalloc byte[2];
                return stream.Read(end) == 2 && header[0] == 0xFF && header[1] == 0xD8
                    && end[0] == 0xFF && end[1] == 0xD9;
            }
            return ValidImageSignature(header.ToArray(), extension);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool ValidImageSignature(byte[] bytes, string extension)
    {
        return extension switch
        {
            ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" => bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8
                && bytes[^2] == 0xFF && bytes[^1] == 0xD9,
            ".gif" => bytes.Length >= 6 && Encoding.ASCII.GetString(bytes, 0, 6)
                is "GIF87a" or "GIF89a",
            ".webp" => bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            _ => false
        };
    }
}
