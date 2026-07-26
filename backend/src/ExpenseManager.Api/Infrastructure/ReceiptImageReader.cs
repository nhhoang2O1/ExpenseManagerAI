namespace ExpenseManager.Api.Infrastructure;

public interface IReceiptImageReader
{
    Task<(byte[] Data, long Size)> ReadAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}

/// <summary>
/// Validates an uploaded receipt and materializes its bytes for the single
/// database transaction that creates Receipt and ReceiptImage.
/// </summary>
public sealed class ReceiptImageReader : IReceiptImageReader
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly IReadOnlySet<string> SupportedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    public async Task<(byte[] Data, long Size)> ReadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!SupportedContentTypes.Contains(file.ContentType))
            throw new InvalidDataException("Only JPEG, PNG, or WebP images are supported.");

        if (file.Length is <= 0 or > MaxFileSize)
            throw new InvalidDataException("The image must be between 1 byte and 10 MB.");

        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream((int)Math.Min(file.Length, MaxFileSize));
        await input.CopyToAsync(buffer, cancellationToken);

        var data = buffer.ToArray();
        if (data.LongLength is 0 or > MaxFileSize)
            throw new InvalidDataException("The image must be between 1 byte and 10 MB.");

        if (!HasValidSignature(file.ContentType, data.AsSpan(0, Math.Min(12, data.Length))))
            throw new InvalidDataException("The file contents do not match the declared image format.");

        return (data, data.LongLength);
    }

    private static bool HasValidSignature(string contentType, ReadOnlySpan<byte> bytes) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 &&
                            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 &&
                           bytes[..8].SequenceEqual(
                               new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytes.Length >= 12 &&
                            bytes[..4].SequenceEqual("RIFF"u8) &&
                            bytes[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
}
