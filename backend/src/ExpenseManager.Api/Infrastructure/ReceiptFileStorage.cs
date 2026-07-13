namespace ExpenseManager.Api.Infrastructure;

public interface IReceiptFileStorage
{
    Task<(string Path, long Size)> SaveAsync(IFormFile file, CancellationToken cancellationToken);
    void Delete(string path);
}

public sealed class ReceiptFileStorage(
    IConfiguration configuration,
    IWebHostEnvironment environment) : IReceiptFileStorage
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> Extensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    public async Task<(string Path, long Size)> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSize)
            throw new InvalidDataException("Ảnh phải có dung lượng từ 1 byte đến 10 MB.");
        if (!Extensions.TryGetValue(file.ContentType, out var extension))
            throw new InvalidDataException("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAsync(header, cancellationToken);
        if (!HasValidSignature(file.ContentType, header.AsSpan(0, bytesRead)))
            throw new InvalidDataException("Nội dung tệp không khớp với định dạng ảnh.");
        input.Position = 0;

        var configuredPath = configuration["Storage:ReceiptPath"] ?? "storage/receipts";
        var root = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using var output = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await input.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            Delete(path);
            throw;
        }

        return (path, file.Length);
    }

    public void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Database deletion is authoritative; stale files can be cleaned separately.
        }
        catch (UnauthorizedAccessException)
        {
        }
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
