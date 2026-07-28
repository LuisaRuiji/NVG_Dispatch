using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NVGInventory.Modules.ShipmentRequests.Services;

public sealed class AzureDocumentIntelligenceOptions
{
    public const string SectionName = "AzureDocumentIntelligence";

    public string? Endpoint { get; init; }
    public string? ApiKey { get; init; }
    public string ModelId { get; init; } = "prebuilt-layout";
    public int PollingTimeoutSeconds { get; init; } = 30;
}

public sealed record AtwExtractionResult(
    bool IsConfigured,
    string? ContainerNumber,
    string? BookingNumber,
    string? ShippingLine,
    decimal Confidence,
    IReadOnlyCollection<string> RiskFlags,
    string? Error = null,
    string? PickupLocation = null,
    string? DropoffLocation = null,
    string? ContainerSize = null,
    string? CargoDescription = null,
    decimal? CargoWeight = null,
    string? SpecialInstructions = null,
    DateTime? RequestedPickupTime = null,
    DateTime? IssueDate = null,
    DateTime? ValidUntil = null);

public interface IAtwDocumentIntelligenceService
{
    Task<AtwExtractionResult> AnalyzeAsync(Stream content, string contentType, CancellationToken cancellationToken = default);
}

public sealed class AzureAtwDocumentIntelligenceService : IAtwDocumentIntelligenceService
{
    private static readonly Regex ContainerNumberPattern = new(
        @"\b([A-Z]{4}\s?[0-9OIL]{7})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BookingNumberPattern = new(
        @"(?:booking\s*(?:no\.?|number|#)?\s*[:#-]?\s*)([A-Z0-9][A-Z0-9/_-]{3,59})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ShippingLinePattern = new(
        @"(?:shipping\s*line|carrier)\s*[:#-]\s*([^\r\n]{2,100})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ContainerSizePattern = new(
        @"\b(20\s*(?:FT|'))\b|\b(40\s*HC)\b|\b(40\s*(?:FT|'))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WeightPattern = new(
        @"(?:cargo|gross|net)?\s*weight\s*(?:\(\s*kg\s*\))?\s*[:#-]?\s*([0-9][0-9,]*(?:\.\d+)?)\s*(?:kg|kgs|kilograms)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly AzureDocumentIntelligenceOptions _options;

    public AzureAtwDocumentIntelligenceService(
        HttpClient httpClient,
        IOptions<AzureDocumentIntelligenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AtwExtractionResult> AnalyzeAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new AtwExtractionResult(false, null, null, null, 0m, Array.Empty<string>(),
                "Azure Document Intelligence is not configured.");
        }

        var endpoint = _options.Endpoint.TrimEnd('/');
        var requestUrl = $"{endpoint}/documentintelligence/documentModels/{Uri.EscapeDataString(_options.ModelId)}:analyze?api-version=2024-11-30";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
        var body = new StreamContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType);
        request.Content = body;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new AtwExtractionResult(true, null, null, null, 0m, Array.Empty<string>(),
                $"Azure analysis request failed ({(int)response.StatusCode}).");
        }

        response.Headers.TryGetValues("Operation-Location", out var operationLocations);
        var operationLocation = operationLocations?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(operationLocation))
        {
            return new AtwExtractionResult(true, null, null, null, 0m, Array.Empty<string>(),
                "Azure did not return an analysis operation.");
        }

        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(_options.PollingTimeoutSeconds, 5, 60));
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            using var statusRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
            statusRequest.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            using var statusResponse = await _httpClient.SendAsync(statusRequest, cancellationToken);
            if (!statusResponse.IsSuccessStatusCode)
            {
                return new AtwExtractionResult(true, null, null, null, 0m, Array.Empty<string>(),
                    $"Azure analysis polling failed ({(int)statusResponse.StatusCode}).");
            }

            using var json = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync(cancellationToken));
            var status = json.RootElement.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                var extractedText = json.RootElement
                    .GetProperty("analyzeResult")
                    .TryGetProperty("content", out var textElement)
                    ? textElement.GetString() ?? string.Empty
                    : string.Empty;
                return Extract(extractedText);
            }

            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return new AtwExtractionResult(true, null, null, null, 0m, Array.Empty<string>(),
                    "Azure could not analyze this document.");
            }
        }

        return new AtwExtractionResult(true, null, null, null, 0m, Array.Empty<string>(),
            "Document analysis timed out. Try uploading a clearer copy.");
    }

    private static AtwExtractionResult Extract(string text)
    {
        var containerNumber = ExtractContainerNumber(text);
        var bookingNumber = FirstNonEmpty(
            BookingNumberPattern.Match(text).Groups[1].Value.Trim(),
            ExtractLabeledValue(text, "booking no.", "booking number", "booking no", "booking"));
        var shippingLine = FirstNonEmpty(
            ShippingLinePattern.Match(text).Groups[1].Value.Trim(' ', ':', '-', '#'),
            ExtractLabeledValue(text, "shipping line", "carrier"));
        var pickupLocation = ExtractLabeledValue(text,
            "pickup location", "place of receipt", "place of withdrawal", "port of loading", "pickup address");
        var dropoffLocation = ExtractLabeledValue(text,
            "dropoff location", "delivery location", "place of delivery", "delivery address", "consignee address");
        var cargoDescription = ExtractLabeledValue(text,
            "cargo description", "commodity", "description of goods", "goods description", "cargo");
        var specialInstructions = ExtractInstructions(text);
        var containerSize = ExtractContainerSize(text);
        var cargoWeight = ExtractWeight(text);
        var requestedPickupTime = ExtractRequestedPickupTime(text);
        var issueDate = ExtractDocumentDate(text, "issue date");
        var validUntil = ExtractDocumentDate(text, "valid until", "expiry date", "expiration date");
        var riskFlags = new List<string>();

        if (string.IsNullOrWhiteSpace(containerNumber)) riskFlags.Add("Container number was not found.");
        if (string.IsNullOrWhiteSpace(bookingNumber)) riskFlags.Add("Booking number was not found.");

        var extractedCount = new[] { containerNumber, bookingNumber, shippingLine, pickupLocation, dropoffLocation, containerSize, cargoDescription }
            .Count(value => !string.IsNullOrWhiteSpace(value));
        var confidence = extractedCount switch
        {
            >= 6 => 0.90m,
            5 => 0.85m,
            4 => 0.75m,
            3 => 0.65m,
            2 => 0.50m,
            1 => 0.35m,
            _ => 0m
        };

        return new AtwExtractionResult(
            true,
            string.IsNullOrWhiteSpace(containerNumber) ? null : containerNumber,
            string.IsNullOrWhiteSpace(bookingNumber) ? null : bookingNumber,
            string.IsNullOrWhiteSpace(shippingLine) ? null : shippingLine,
            confidence,
            riskFlags,
            PickupLocation: pickupLocation,
            DropoffLocation: dropoffLocation,
            ContainerSize: containerSize,
            CargoDescription: cargoDescription,
            CargoWeight: cargoWeight,
            SpecialInstructions: specialInstructions,
            RequestedPickupTime: requestedPickupTime,
            IssueDate: issueDate,
            ValidUntil: validUntil);
    }

    private static string? ExtractContainerNumber(string text)
    {
        var directMatch = ContainerNumberPattern.Match(text).Groups[1].Value;
        var labeledValue = ExtractLabeledValue(text, "container no.", "container number", "container no");
        return NormalizeContainerNumber(directMatch) ?? NormalizeContainerNumber(labeledValue);
    }

    private static string? NormalizeContainerNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (compact.Length != 11 || !compact[..4].All(char.IsLetter))
        {
            return null;
        }

        var number = compact[4..]
            .Replace('O', '0')
            .Replace('I', '1')
            .Replace('L', '1');
        return number.All(char.IsDigit) ? compact[..4] + number : null;
    }

    private static string? ExtractLabeledValue(string text, params string[] labels)
    {
        var pattern = string.Join("|", labels.Select(Regex.Escape));
        var match = Regex.Match(
            text,
            $@"(?:^|\r?\n)\s*(?:{pattern})\s*(?:[:#-]|\|)\s*(?<value>[^\r\n|]{{2,180}})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value.Trim(' ', ':', '-', '#');
        return value.Length is >= 2 and <= 180 ? value : null;
    }

    private static string? ExtractContainerSize(string text)
    {
        var value = ContainerSizePattern.Match(text).Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return value switch
        {
            "20FT" or "20'" => "TWENTY_FT",
            "40FT" or "40'" => "FORTY_FT",
            "40HC" => "FORTY_HC",
            _ => null
        };
    }

    private static decimal? ExtractWeight(string text)
    {
        var raw = WeightPattern.Match(text).Groups[1].Value.Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight) && weight >= 0
            ? weight
            : null;
    }

    private static string? ExtractInstructions(string text)
    {
        var match = Regex.Match(
            text,
            @"(?:^|\r?\n)\s*special\s*instructions\s*[:#-]\s*(?<value>[^\r\n]{2,350})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim(' ', ':', '-', '#') : null;
    }

    private static DateTime? ExtractRequestedPickupTime(string text)
    {
        var match = Regex.Match(
            text,
            @"(?:requested\s*pickup(?:\s*(?:date|time|schedule)(?:\s*(?:&|and)\s*time)?)?|pickup\s*(?:date|time|schedule))\s*(?:[:#-]|\|)\s*(?<value>[^\r\n|]{6,80})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value.Trim();
        var formats = new[]
        {
            "d MMMM yyyy h:mm tt", "dd MMMM yyyy h:mm tt",
            "d MMM yyyy h:mm tt", "dd MMM yyyy h:mm tt",
            "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm"
        };
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var pickupTime)
            ? pickupTime
            : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static DateTime? ExtractDocumentDate(string text, params string[] labels)
    {
        var value = ExtractLabeledValue(text, labels);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "d MMMM yyyy", "dd MMMM yyyy", "d MMM yyyy", "dd MMM yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var date)
            ? date.Date
            : null;
    }
}
