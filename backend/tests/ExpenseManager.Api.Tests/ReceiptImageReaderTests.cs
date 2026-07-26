using ExpenseManager.Api.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace ExpenseManager.Api.Tests;

public sealed class ReceiptImageReaderTests
{
    [Fact]
    public async Task ReadAsync_returns_exact_png_bytes()
    {
        var bytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x01, 0x02, 0x03
        };
        var reader = new ReceiptImageReader();

        var result = await reader.ReadAsync(
            CreateFile(bytes, "image/png", "receipt.png"),
            CancellationToken.None);

        Assert.Equal(bytes, result.Data);
        Assert.Equal(bytes.LongLength, result.Size);
    }

    [Fact]
    public async Task ReadAsync_rejects_mismatched_magic_bytes()
    {
        var reader = new ReceiptImageReader();

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(
            CreateFile([1, 2, 3], "image/png", "receipt.png"),
            CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_rejects_files_larger_than_ten_megabytes()
    {
        var reader = new ReceiptImageReader();
        var bytes = new byte[10 * 1024 * 1024 + 1];

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(
            CreateFile(bytes, "image/jpeg", "receipt.jpg"),
            CancellationToken.None));
    }

    private static FormFile CreateFile(byte[] bytes, string contentType, string name)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
