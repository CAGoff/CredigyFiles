using SecureFileTransfer.Api.Models;
using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class FileValidationServiceTests
{
    // --- Directory validation ---

    [Theory]
    [InlineData("inbound", true)]
    [InlineData("outbound", true)]
    [InlineData("Inbound", false)]
    [InlineData("OUTBOUND", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("../inbound", false)]
    [InlineData("inbound/subdir", false)]
    public void IsValidDirectory_ValidatesStrictly(string? dir, bool expected)
    {
        Assert.Equal(expected, FileValidationService.IsValidDirectory(dir));
    }

    // --- Filename sanitization ---

    [Fact]
    public void SanitizeFileName_StripsPathComponents()
    {
        Assert.Equal("report.pdf", FileValidationService.SanitizeFileName("C:\\Users\\test\\report.pdf"));
        Assert.Equal("report.pdf", FileValidationService.SanitizeFileName("/etc/secret/report.pdf"));
        Assert.Equal("report.pdf", FileValidationService.SanitizeFileName("../../../report.pdf"));
    }

    [Fact]
    public void SanitizeFileName_ReplacesSpecialChars()
    {
        Assert.Equal("my_report__2024_.pdf", FileValidationService.SanitizeFileName("my report (2024).pdf"));
    }

    [Fact]
    public void SanitizeFileName_AllowsValidChars()
    {
        Assert.Equal("report-2024_v2.pdf", FileValidationService.SanitizeFileName("report-2024_v2.pdf"));
    }

    [Fact]
    public void SanitizeFileName_RejectsEmpty()
    {
        Assert.Null(FileValidationService.SanitizeFileName(null));
        Assert.Null(FileValidationService.SanitizeFileName(""));
        Assert.Null(FileValidationService.SanitizeFileName("   "));
    }

    [Fact]
    public void SanitizeFileName_RejectsDotsOnly()
    {
        Assert.Null(FileValidationService.SanitizeFileName("..."));
        Assert.Null(FileValidationService.SanitizeFileName(".."));
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 300) + ".pdf";
        var result = FileValidationService.SanitizeFileName(longName);
        Assert.NotNull(result);
        Assert.True(result.Length <= 255);
    }

    // --- Extension validation ---

    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData("data.csv", true)]
    [InlineData("sheet.xlsx", true)]
    [InlineData("old.xls", true)]
    [InlineData("notes.txt", true)]
    [InlineData("script.exe", false)]
    [InlineData("hack.ps1", false)]
    [InlineData("image.png", false)]
    [InlineData("noextension", false)]
    public void HasAllowedExtension_ChecksAllowlist(string fileName, bool expected)
    {
        var options = new FileValidationOptions();
        Assert.Equal(expected, FileValidationService.HasAllowedExtension(fileName, options));
    }

    [Fact]
    public void HasAllowedExtension_IsCaseInsensitive()
    {
        var options = new FileValidationOptions();
        Assert.True(FileValidationService.HasAllowedExtension("report.PDF", options));
        Assert.True(FileValidationService.HasAllowedExtension("data.CSV", options));
    }

    // --- Magic byte validation ---

    [Fact]
    public async Task ValidateMagicBytesAsync_ValidPdf_ReturnsTrue()
    {
        // %PDF magic bytes
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        using var stream = new MemoryStream(content);

        Assert.True(await FileValidationService.ValidateMagicBytesAsync(stream, "report.pdf"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_InvalidPdf_ReturnsFalse()
    {
        // Not PDF magic bytes
        var content = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        using var stream = new MemoryStream(content);

        Assert.False(await FileValidationService.ValidateMagicBytesAsync(stream, "report.pdf"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_ValidXlsx_ReturnsTrue()
    {
        // PK (ZIP) magic bytes
        var content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(content);

        Assert.True(await FileValidationService.ValidateMagicBytesAsync(stream, "sheet.xlsx"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_ValidXls_ReturnsTrue()
    {
        // OLE2 magic bytes
        var content = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        using var stream = new MemoryStream(content);

        Assert.True(await FileValidationService.ValidateMagicBytesAsync(stream, "data.xls"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_CsvWithAnyContent_ReturnsTrue()
    {
        // CSV has no magic bytes — any content should pass
        var content = "Name,Value\nFoo,Bar"u8.ToArray();
        using var stream = new MemoryStream(content);

        Assert.True(await FileValidationService.ValidateMagicBytesAsync(stream, "data.csv"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_TxtWithAnyContent_ReturnsTrue()
    {
        var content = "Hello world"u8.ToArray();
        using var stream = new MemoryStream(content);

        Assert.True(await FileValidationService.ValidateMagicBytesAsync(stream, "notes.txt"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_TooFewBytes_ReturnsFalse()
    {
        var content = new byte[] { 0x25, 0x50 }; // Only 2 bytes
        using var stream = new MemoryStream(content);

        Assert.False(await FileValidationService.ValidateMagicBytesAsync(stream, "report.pdf"));
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_ResetsStreamPosition()
    {
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 };
        using var stream = new MemoryStream(content);
        stream.Position = 0;

        await FileValidationService.ValidateMagicBytesAsync(stream, "report.pdf");

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ValidateMagicBytesAsync_DisguisedExe_ReturnsFalse()
    {
        // MZ header (PE executable) disguised as .pdf
        var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(content);

        Assert.False(await FileValidationService.ValidateMagicBytesAsync(stream, "report.pdf"));
    }
}
