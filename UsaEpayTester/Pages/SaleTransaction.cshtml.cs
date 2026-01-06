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

            // USAePay REST expects nested objects like:
            //   { "creditcard": { "number": "...", "expiration": "...", "cvc": "..." } }
            // This app originally shipped with a "flattened" sample payload; normalize it for convenience.
            if (requestToken is JObject requestObject)
            {
                NormalizeCreditCardShape(requestObject);
            }

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

    private static void NormalizeCreditCardShape(JObject request)
    {
        // If "creditcard" is already an object, do nothing.
        // If it's a string (card number), move related top-level fields into a nested object to match the REST docs.
        if (request["creditcard"] is not JValue creditCardValue || creditCardValue.Type != JTokenType.String)
        {
            return;
        }

        var number = (string?)creditCardValue;
        if (string.IsNullOrWhiteSpace(number))
        {
            return;
        }

        var creditcard = new JObject
        {
            ["number"] = number.Trim()
        };

        // Common "flattened" field names (older examples / UI habits) -> REST doc keys.
        if (request.TryGetValue("cardholder", StringComparison.OrdinalIgnoreCase, out var cardholder))
        {
            creditcard["cardholder"] = cardholder;
            request.Remove("cardholder");
        }

        if (request.TryGetValue("exp", StringComparison.OrdinalIgnoreCase, out var exp))
        {
            creditcard["expiration"] = exp;
            request.Remove("exp");
        }
        else if (request.TryGetValue("expiration", StringComparison.OrdinalIgnoreCase, out var expiration))
        {
            creditcard["expiration"] = expiration;
            request.Remove("expiration");
        }

        if (request.TryGetValue("cvv2", StringComparison.OrdinalIgnoreCase, out var cvv2))
        {
            creditcard["cvc"] = cvv2;
            request.Remove("cvv2");
        }
        else if (request.TryGetValue("cvc", StringComparison.OrdinalIgnoreCase, out var cvc))
        {
            creditcard["cvc"] = cvc;
            request.Remove("cvc");
        }

        // These are commonly entered as "street"/"zip" but REST expects "avs_street"/"avs_zip" inside creditcard.
        if (request.TryGetValue("street", StringComparison.OrdinalIgnoreCase, out var street))
        {
            creditcard["avs_street"] = street;
            request.Remove("street");
        }

        if (request.TryGetValue("zip", StringComparison.OrdinalIgnoreCase, out var zip))
        {
            creditcard["avs_zip"] = zip;
            request.Remove("zip");
        }

        request["creditcard"] = creditcard;
    }

    private const string DefaultRequestJson =
        """
        {
          "command": "sale",
          "amount": "1.00",
          "invoice": "INV-1001",
          "description": "Test sale via UsaEpayTester",
          "creditcard": {
            "cardholder": "Test Customer",
            "number": "4111111111111111",
            "expiration": "1228",
            "cvc": "999",
            "avs_street": "1 Main St",
            "avs_zip": "90210"
          }
        }
        """;
}

