using SoftwareFactory.Infrastructure.Persistence.Conventions;

namespace SoftwareFactory.Infrastructure.Tests.Persistence.Conventions;

public sealed class SnakeCaseTests
{
    [Theory]
    [InlineData("Id", "id")]
    [InlineData("TenantId", "tenant_id")]
    [InlineData("NormalizedEmail", "normalized_email")]
    [InlineData("LlmCall", "llm_call")]
    [InlineData("LatencyMs", "latency_ms")]
    [InlineData("TwoFactorEnabled", "two_factor_enabled")]
    [InlineData("SizeBytes", "size_bytes")]
    [InlineData("InReview", "in_review")]
    [InlineData("OutOfRoleNote", "out_of_role_note")]
    [InlineData("xmin", "xmin")]
    [InlineData("Qa", "qa")]
    public void Converts_pascal_case_to_snake_case(string input, string expected) =>
        Assert.Equal(expected, SnakeCase.Convert(input));
}
