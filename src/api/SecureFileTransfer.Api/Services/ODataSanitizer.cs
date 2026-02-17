namespace SecureFileTransfer.Api.Services;

public static class ODataSanitizer
{
    /// <summary>
    /// Escapes a string value for safe use in OData filter expressions.
    /// Single quotes are doubled per the OData specification.
    /// </summary>
    public static string EscapeStringValue(string value) =>
        value.Replace("'", "''");
}
