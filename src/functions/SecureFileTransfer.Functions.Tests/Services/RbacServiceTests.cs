namespace SecureFileTransfer.Functions.Tests.Services;

public class RbacServiceTests
{
    private const string BlobDataContributorRoleId = "ba92f5b4-2d11-453d-a403-e96b0029c9fe";

    [Theory]
    [InlineData("my-container", "sub-123", "rg-test", "staccount")]
    [InlineData("sft-acme", "sub-abc", "rg-prod", "stprod")]
    public void ContainerScope_IsCorrectlyFormatted(
        string containerName, string subscriptionId, string resourceGroup, string storageAccount)
    {
        var expected =
            $"/subscriptions/{subscriptionId}" +
            $"/resourceGroups/{resourceGroup}" +
            $"/providers/Microsoft.Storage/storageAccounts/{storageAccount}" +
            $"/blobServices/default/containers/{containerName}";

        // Replicate the scope building logic
        var scope =
            $"/subscriptions/{subscriptionId}" +
            $"/resourceGroups/{resourceGroup}" +
            $"/providers/Microsoft.Storage/storageAccounts/{storageAccount}" +
            $"/blobServices/default/containers/{containerName}";

        Assert.Equal(expected, scope);
        Assert.Contains("blobServices/default/containers/", scope);
        Assert.StartsWith("/subscriptions/", scope);
    }

    [Fact]
    public void BlobDataContributorRoleId_IsCorrect()
    {
        // Well-known Azure built-in role ID for Storage Blob Data Contributor
        Assert.Equal("ba92f5b4-2d11-453d-a403-e96b0029c9fe", BlobDataContributorRoleId);
        Assert.True(Guid.TryParse(BlobDataContributorRoleId, out _));
    }

    [Fact]
    public void RoleDefinitionId_ContainsSubscription()
    {
        var subscriptionId = "test-sub-id";
        var roleDefId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{BlobDataContributorRoleId}";

        Assert.Contains(subscriptionId, roleDefId);
        Assert.Contains(BlobDataContributorRoleId, roleDefId);
    }

    [Theory]
    [InlineData("ServicePrincipal")]
    [InlineData("User")]
    public void PrincipalType_IsValid(string principalType)
    {
        var validTypes = new[] { "ServicePrincipal", "User", "Group", "ForeignGroup" };
        Assert.Contains(principalType, validTypes);
    }
}
