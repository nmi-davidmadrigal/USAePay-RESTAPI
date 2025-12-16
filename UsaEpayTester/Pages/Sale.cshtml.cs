using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using USAePay;

namespace UsaEpayTester.Pages;

/// <summary>
/// Beginner-friendly "Sale" transaction tester page.
///
/// This uses the official USAePay .NET SDK (NuGet package: USAePAY.SDK).
///
/// Docs:
/// - SDK setup: https://help.usaepay.info/api/rest/#net-guide
/// - Sale transaction: https://help.usaepay.info/api/rest/#sale
/// </summary>
public class SaleModel : PageModel
{
    // ----- Inputs (form fields) -----

    /// <summary>
    /// Which server to call. Use sandbox while developing.
    /// NOTE: The SDK wants the "base host" WITHOUT "/api/v2" because it appends "/api/{endpoint}" itself.
    /// </summary>
    [BindProperty]
    public string BaseHost { get; set; } = "https://sandbox.usaepay.com";

    /// <summary>
    /// API endpoint key / version. Most beginners start with "v2".
    /// </summary>
    [BindProperty]
    public string EndpointKey { get; set; } = "v2";

    /// <summary>
    /// Your API key from the USAePay merchant console.
    /// </summary>
    [BindProperty]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Your API PIN associated with the API key.
    /// </summary>
    [BindProperty]
    public string ApiPin { get; set; } = "";

    /// <summary>
    /// Total amount for the sale (Required by the API).
    /// </summary>
    [BindProperty]
    public decimal Amount { get; set; } = 1.00m;

    /// <summary>
    /// Optional invoice number. This can help you look up a transaction later.
    /// </summary>
    [BindProperty]
    public string? Invoice { get; set; }

    /// <summary>
    /// Optional description shown on transaction record/receipt.
    /// </summary>
    [BindProperty]
    public string? Description { get; set; }

    // Credit card fields (inside the API's "creditcard" object)
    [BindProperty]
    public string Cardholder { get; set; } = "";

    [BindProperty]
    public string CardNumber { get; set; } = "";

    /// <summary>
    /// Card expiration in MMYY format (example: 0426).
    /// </summary>
    [BindProperty]
    public string ExpirationMmyy { get; set; } = "";

    [BindProperty]
    public string? Cvc { get; set; }

    [BindProperty]
    public string? AvsStreet { get; set; }

    [BindProperty]
    public string? AvsZip { get; set; }

    /// <summary>
    /// If true, tells the gateway to ignore duplicates (helpful while testing).
    /// </summary>
    [BindProperty]
    public bool IgnoreDuplicate { get; set; } = true;

    // ----- Outputs (shown after submitting) -----
    public string? RequestJsonForDisplay { get; private set; }
    public string? ResponseJson { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        // Render the form.
    }

    public void OnPostClear()
    {
        // Reset the output panel.
        RequestJsonForDisplay = null;
        ResponseJson = null;
        ErrorMessage = null;
    }

    public void OnPostSubmit()
    {
        try
        {
            // 1) Configure the SDK:
            //    - SetURL chooses the server (sandbox vs production) + endpoint key (usually "v2").
            //    - SetAuthentication sets your API key + PIN (required before making calls).
            API.SetURL(NormalizeBaseHost(BaseHost), EndpointKey.Trim());
            API.SetAuthentication(ApiKey.Trim(), ApiPin.Trim());

            // 2) Build the request body for:
            //    POST /api/v2/transactions
            // with "command": "sale"
            //
            // The docs show the SDK accepts a Dictionary<string, object> as the request payload.
            // (This is nice for beginners because you can mirror the JSON structure.)
            var request = new Dictionary<string, object>
            {
                // Required:
                ["command"] = "sale",
                ["amount"] = Amount.ToString("0.00"),

                // Helpful while testing:
                ["ignore_duplicate"] = IgnoreDuplicate ? 1 : 0
            };

            // Optional top-level fields:
            if (!string.IsNullOrWhiteSpace(Invoice))
            {
                request["invoice"] = Invoice.Trim();
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                request["description"] = Description.Trim();
            }

            // 3) Add the required "creditcard" object.
            // Minimum fields you normally need for a keyed card sale are:
            // - number
            // - expiration (MMYY)
            //
            // (Some processors may also require cvc and/or AVS fields depending on settings.)
            var creditcard = new Dictionary<string, object>
            {
                ["number"] = CardNumber.Trim(),
                ["expiration"] = ExpirationMmyy.Trim()
            };

            if (!string.IsNullOrWhiteSpace(Cardholder))
            {
                creditcard["cardholder"] = Cardholder.Trim();
            }

            if (!string.IsNullOrWhiteSpace(Cvc))
            {
                creditcard["cvc"] = Cvc.Trim();
            }

            if (!string.IsNullOrWhiteSpace(AvsStreet))
            {
                creditcard["avs_street"] = AvsStreet.Trim();
            }

            if (!string.IsNullOrWhiteSpace(AvsZip))
            {
                creditcard["avs_zip"] = AvsZip.Trim();
            }

            request["creditcard"] = creditcard;

            // 4) Display the request JSON (with the card number masked so it's safer to view/copy).
            RequestJsonForDisplay = JsonConvert.SerializeObject(MaskCardNumberForDisplay(request), Formatting.Indented);

            // 5) Call the USAePay API using the SDK.
            // The SDK will throw a USAePay.APIException for many API problems (missing fields, auth errors, etc.).
            var response = API.Transactions.Post(request);

            // 6) Show the response as pretty JSON.
            ResponseJson = JsonConvert.SerializeObject(response, Formatting.Indented);
        }
        catch (Exception ex)
        {
            // We show the message on-screen instead of crashing the app.
            ErrorMessage = ex.Message;
        }
    }

    private static string NormalizeBaseHost(string baseHost)
    {
        // The SDK expects something like "https://sandbox.usaepay.com" (no trailing "/api/v2").
        // If a beginner pastes the full base URL from the REST docs, we trim it down safely.
        var trimmed = baseHost.Trim().TrimEnd('/');

        // Common copy/paste value: https://sandbox.usaepay.com/api/v2/
        if (trimmed.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"/api/v2".Length];
        }

        // Common copy/paste value: https://sandbox.usaepay.com/api/v2
        if (trimmed.EndsWith("/api/v2", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"/api/v2".Length];
        }

        return trimmed;
    }

    private static IDictionary<string, object> MaskCardNumberForDisplay(IDictionary<string, object> request)
    {
        // Create a shallow copy so we don't change what we actually send to the API.
        var copy = new Dictionary<string, object>(request);

        if (copy.TryGetValue("creditcard", out var ccObj) && ccObj is IDictionary<string, object> ccDict)
        {
            var ccCopy = new Dictionary<string, object>(ccDict);
            if (ccCopy.TryGetValue("number", out var numberObj) && numberObj is string number)
            {
                ccCopy["number"] = MaskPan(number);
            }

            copy["creditcard"] = ccCopy;
        }

        return copy;
    }

    private static string MaskPan(string pan)
    {
        // PAN = Primary Account Number (the card number).
        // We show only the last 4 digits.
        var digits = new string(pan.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return "****";
        }

        return new string('x', digits.Length - 4) + digits[^4..];
    }
}

