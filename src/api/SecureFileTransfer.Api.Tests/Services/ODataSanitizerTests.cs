using SecureFileTransfer.Api.Services;

namespace SecureFileTransfer.Api.Tests.Services;

public class ODataSanitizerTests
{
    [Fact]
    public void EscapeStringValue_NoQuotes_ReturnsUnchanged()
    {
        Assert.Equal("sft-acme", ODataSanitizer.EscapeStringValue("sft-acme"));
    }

    [Fact]
    public void EscapeStringValue_SingleQuote_IsDoubled()
    {
        Assert.Equal("foo''bar", ODataSanitizer.EscapeStringValue("foo'bar"));
    }

    [Fact]
    public void EscapeStringValue_InjectionAttempt_IsEscaped()
    {
        var malicious = "foo' or 1 eq 1 or '";
        var escaped = ODataSanitizer.EscapeStringValue(malicious);
        Assert.Equal("foo'' or 1 eq 1 or ''", escaped);
    }

    [Fact]
    public void EscapeStringValue_MultipleQuotes_AllDoubled()
    {
        Assert.Equal("a''b''c", ODataSanitizer.EscapeStringValue("a'b'c"));
    }

    [Fact]
    public void EscapeStringValue_Empty_ReturnsEmpty()
    {
        Assert.Equal("", ODataSanitizer.EscapeStringValue(""));
    }
}
