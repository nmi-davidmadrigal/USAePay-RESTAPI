using System.Security.Cryptography;
using System.Text;

namespace UsaEpayTester.Services;

/// <summary>
/// Helper functions for creating the USAePay REST API authentication header.
///
/// The USAePay REST docs explain that every API call uses HTTP Basic Auth:
///   Authorization: Basic base64( apiKey:apiHash )
///
/// Where apiHash is built like this:
///   seed = random_value()
///   prehash = apiKey + seed + apiPin
///   apiHash = "s2/" + seed + "/" + sha256(prehash)
///
/// Source (docs): https://help.usaepay.info/api/rest/#authentication
/// </summary>
public static class UsaEpayAuthHeader
{
    private const string SeedAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Generates a random alphanumeric "seed" string.
    /// The seed becomes part of the apiHash string, so we avoid characters like "/" that would break the format.
    /// </summary>
    public static string GenerateSeed(int length = 16)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Seed length must be greater than 0.");
        }

        // RandomNumberGenerator is the recommended cryptographically-secure random API in .NET.
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (var i = 0; i < length; i++)
        {
            chars[i] = SeedAlphabet[bytes[i] % SeedAlphabet.Length];
        }

        return new string(chars);
    }

    /// <summary>
    /// Creates the USAePay "apiHash" value: s2/{seed}/{sha256(apiKey + seed + apiPin)}.
    /// </summary>
    public static string CreateApiHash(string apiKey, string apiPin, string seed)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(apiPin))
        {
            throw new ArgumentException("API PIN is required.", nameof(apiPin));
        }

        if (string.IsNullOrWhiteSpace(seed))
        {
            throw new ArgumentException("Seed is required.", nameof(seed));
        }

        // prehash = apiKey + seed + apiPin (exactly as described in the docs)
        var prehash = apiKey + seed + apiPin;

        // sha256(prehash) as lowercase hex (common textual format for hashes).
        var digestBytes = SHA256.HashData(Encoding.UTF8.GetBytes(prehash));
        var digestHex = Convert.ToHexString(digestBytes).ToLowerInvariant();

        return $"s2/{seed}/{digestHex}";
    }

    /// <summary>
    /// Creates the final HTTP Basic Authentication "parameter" (the part after "Basic ").
    /// This is base64( apiKey:apiHash ).
    /// </summary>
    public static string CreateBasicAuthParameter(string apiKey, string apiHash)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key is required.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(apiHash))
        {
            throw new ArgumentException("API hash is required.", nameof(apiHash));
        }

        var raw = $"{apiKey}:{apiHash}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }
}

