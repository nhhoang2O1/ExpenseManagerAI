using System.Net;
using System.Text;
using ExpenseManager.Api.Infrastructure;

namespace ExpenseManager.Api.Tests;

public sealed class OcrClientTests
{
    [Fact]
    public async Task ProcessAsync_UsesInternalEndpointAndImageMultipartField()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://ocr-service:8000/")
        };
        var client = new OcrClient(httpClient);
        var image = new byte[] { 1, 2, 3 };

        var result = await client.ProcessAsync(
            image,
            "receipt.jpg",
            "image/jpeg",
            CancellationToken.None);

        Assert.Equal(
            new Uri("http://ocr-service:8000/internal/v1/ocr/receipts"),
            handler.RequestUri);
        Assert.Equal("image", handler.MultipartFieldName);
        Assert.Equal(image, handler.ImageBytes);
        Assert.Equal("image/jpeg", handler.ImageContentType);
        Assert.Equal("Circle K", result.Fields.StoreName);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? MultipartFieldName { get; private set; }
        public byte[]? ImageBytes { get; private set; }
        public string? ImageContentType { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var part = Assert.Single(multipart);
            MultipartFieldName = part.Headers.ContentDisposition?.Name?.Trim('"');
            ImageBytes = part.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            ImageContentType = part.Headers.ContentType?.MediaType;

            const string json = """
                {
                  "classification": "SUPPORTED",
                  "status": "REVIEW_REQUIRED",
                  "rawText": "CIRCLE K",
                  "lines": [],
                  "fields": {
                    "storeName": "Circle K",
                    "receiptDate": "2026-07-09",
                    "totalAmount": 125000,
                    "vatAmount": null
                  },
                  "overallConfidence": 0.95,
                  "modelVersion": "test-model",
                  "parserVersion": "test-parser",
                  "warnings": [],
                  "processingTimeMs": 25
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
