using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UsaEpayTester.Services;

namespace UsaEpayTester.Pages;

/// <summary>
/// This page demonstrates how USAePay REST authentication works.
///
/// You enter an API Key + API PIN, and we generate:
/// - seed
/// - apiHash
/// - Authorization header value (Basic base64(apiKey:apiHash))
///
/// Then (optionally) we make a simple GET request so you can confirm the credentials are accepted.
/// </summary>
public class AuthModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // ----- Inputs (what the user types into the form) -----

    /// <summary>
    /// The API base URL (sandbox or production).
    /// Docs: https://help.usaepay.info/api/rest/#base-url
    /// </summary>
    [BindProperty]
    public string BaseUrl { get; set; } = "https://sandbox.usaepay.com/api/v2/";

    /// <summary>
    /// The API endpoint we will call after generating the Authorization header.
    /// This is just for testing; you can change it.
    /// Example: "transactions?limit=1"
    /// </summary>
    [BindProperty]
    public string TestRelativePath { get; set; } = "transactions?limit=1";

    /// <summary>
    /// Your USAePay API key (sometimes called "sourcekey" in older docs).
    /// </summary>
    [BindProperty]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Your API PIN associated with the API key.
    /// </summary>
    [BindProperty]
    public string ApiPin { get; set; } = "";

    /// <summary>
    /// Optional: provide your own seed. If left blank, we generate one.
    /// </summary>
    [BindProperty]
    public string? SeedOverride { get; set; }

    // ----- Outputs (what we show after clicking "Generate") -----

    public string? SeedUsed { get; private set; }
    public string? ApiHash { get; private set; }
    public string? BasicAuthParameter { get; private set; }
    public string? AuthorizationHeader { get; private set; }

    public int? HttpStatusCode { get; private set; }
    public string? HttpResponseBody { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        // No work needed. We just render the form.
    }

    public async Task OnPostGenerateAsync()
    {
        try
        {
            // 1) Pick a seed.
            SeedUsed = string.IsNullOrWhiteSpace(SeedOverride)
                ? UsaEpayAuthHeader.GenerateSeed()
                : SeedOverride.Trim();

            // 2) Create the apiHash string using the algorithm from the docs.
            ApiHash = UsaEpayAuthHeader.CreateApiHash(ApiKey.Trim(), ApiPin.Trim(), SeedUsed);

            // 3) Build the "Basic <base64(...)>" value.
            BasicAuthParameter = UsaEpayAuthHeader.CreateBasicAuthParameter(ApiKey.Trim(), ApiHash);
            AuthorizationHeader = $"Basic {BasicAuthParameter}";

            // 4) Make a simple request so you can see if auth works.
            // NOTE: The endpoint you choose determines what data is returned.
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(EnsureTrailingSlash(BaseUrl.Trim()));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuthParameter);

            var response = await client.GetAsync(TestRelativePath.Trim().TrimStart('/'));
            HttpStatusCode = (int)response.StatusCode;

            // Read the response body as text so we can display it on the page.
            // If the response isn't JSON, you'll still see something useful.
            HttpResponseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            // If anything goes wrong (invalid URL, bad credentials, network error),
            // we show the message on the page instead of crashing.
            ErrorMessage = ex.Message;
        }
    }

    private static string EnsureTrailingSlash(string url)
    {
        // If the user enters "https://sandbox.usaepay.com/api/v2", we turn it into ".../api/v2/".
        return url.EndsWith("/") ? url : url + "/";
    }
}

