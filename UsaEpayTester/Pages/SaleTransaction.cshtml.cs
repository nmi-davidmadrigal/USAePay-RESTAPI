using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UsaEpayTester.Services;

namespace UsaEpayTester.Pages;

public class SaleTransactionModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SaleTransactionModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    // ----- Inputs -----

    [BindProperty]
    public string BaseUrl { get; set; } = "https://sandbox.usaepay.com/api/v2/";

    [BindProperty]
    public string ApiKey { get; set; } = "";

    [BindProperty]
    public string ApiPin { get; set; } = "";

    [BindProperty]
    public string? SeedOverride { get; set; }

    [BindProperty]
    public string RequestJson { get; set; } = DefaultRequestJson;

    // ----- Outputs -----

    public string? SeedUsed { get; private set; }
    public string? ApiHash { get; private set; }
    public string? RequestJsonSent { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? HttpResponseBody { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        // Render form.
    }

    public async Task OnPostSubmitAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                throw new InvalidOperationException("Base URL is required.");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException("API Key is required.");
            }

            if (string.IsNullOrWhiteSpace(ApiPin))
            {
                throw new InvalidOperationException("API PIN is required.");
            }

            if (string.IsNullOrWhiteSpace(RequestJson))
            {
                throw new InvalidOperationException("Request JSON is required.");
            }

            // Validate and normalize the JSON before sending.
            // If you want to keep it exactly as typed, change RequestJsonSent to RequestJson.Trim().
            var requestToken = JToken.Parse(RequestJson);
            RequestJsonSent = requestToken.ToString(Formatting.Indented);

            // Build auth header (same algorithm as the Auth page).
            SeedUsed = string.IsNullOrWhiteSpace(SeedOverride)
                ? UsaEpayAuthHeader.GenerateSeed()
                : SeedOverride.Trim();

            ApiHash = UsaEpayAuthHeader.CreateApiHash(ApiKey.Trim(), ApiPin.Trim(), SeedUsed);
            var basicAuthParameter = UsaEpayAuthHeader.CreateBasicAuthParameter(ApiKey.Trim(), ApiHash);

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(EnsureTrailingSlash(BaseUrl.Trim()));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuthParameter);

            using var request = new HttpRequestMessage(HttpMethod.Post, "transactions");
            request.Content = new StringContent(RequestJsonSent, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            HttpStatusCode = (int)response.StatusCode;

            var responseText = await response.Content.ReadAsStringAsync();
            HttpResponseBody = TryFormatJson(responseText);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith("/") ? url : url + "/";

    private static string TryFormatJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            var token = JToken.Parse(text);
            return token.ToString(Formatting.Indented);
        }
        catch
        {
            // Not JSON (or invalid JSON) - just show it as-is.
            return text;
        }
    }

    private const string DefaultRequestJson =
        """
        {
          "command": "sale",
          "amount": "1.00",
          "invoice": "INV-1001",
          "description": "Test sale via UsaEpayTester",
          "creditcard": "4111111111111111",
          "exp": "1228",
          "cvv2": "999",
          "cardholder": "Test Customer",
          "street": "1 Main St",
          "zip": "90210"
        }
        """;
}

