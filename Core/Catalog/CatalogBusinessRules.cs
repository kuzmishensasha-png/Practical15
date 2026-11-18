namespace Practical1.Core.Catalog;

public static class CatalogBusinessRules
{
    public static string RequiredText(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{field} cannot be empty.");
        }

        return text.Trim();
    }

    public static int ValidRarity(int rarity)
    {
        if (rarity < 1 || rarity > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rarity),
                "Rarity must be from 1 to 5.");
        }

        return rarity;
    }

    public static string ValidImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return "";
        }

        string text = imageUrl.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Image URL must use http or https.");
        }

        return uri.ToString();
    }

    public static string RemoteImageUrl(string id)
    {
        return "";
    }
}
