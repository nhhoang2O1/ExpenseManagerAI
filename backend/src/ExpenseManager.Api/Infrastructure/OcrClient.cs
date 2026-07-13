using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseManager.Api.Domain;

namespace ExpenseManager.Api.Infrastructure;

public sealed record OcrLine(
    string Text,
    decimal Confidence,
    JsonElement Box);

public sealed record OcrFields(
    string? StoreName,
    DateOnly? ReceiptDate,
    long? TotalAmount,
    long? VatAmount);

public sealed record OcrServiceResponse(
    ReceiptClassification Classification,
    string Status,
    string RawText,
    IReadOnlyList<OcrLine> Lines,
    OcrFields Fields,
    decimal OverallConfidence,
    string ModelVersion,
    string ParserVersion,
    IReadOnlyList<string> Warnings,
    long ProcessingTimeMs);

public interface IOcrClient
{
    Task<OcrServiceResponse> ProcessAsync(
        string filePath,
        string contentType,
        CancellationToken cancellationToken);
}

public sealed class OcrClient(HttpClient httpClient) : IOcrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<OcrServiceResponse> ProcessAsync(
        string filePath,
        string contentType,
        CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(filePath);
        using var fileContent = new StreamContent(file);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "image", Path.GetFileName(filePath));

        using var response = await httpClient.PostAsync(
            "internal/v1/ocr/receipts",
            form,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"OCR service returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);

        return JsonSerializer.Deserialize<OcrServiceResponse>(body, JsonOptions)
               ?? throw new InvalidDataException("OCR service returned an empty response.");
    }
}
