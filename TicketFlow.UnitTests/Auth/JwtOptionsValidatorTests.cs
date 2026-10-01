using TicketFlow.Application.Auth;

namespace TicketFlow.UnitTests.Auth;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    private static JwtOptions ValidOptions() => new()
    {
        Secret = new string('s', 32),
        Issuer = "TicketFlow",
        Audience = "TicketFlow",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        Assert.True(_validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curto")]
    [InlineData("31-caracteres-aaaaaaaaaaaaaaaa")]
    public void Validate_WithMissingOrShortSecret_Fails(string secret)
    {
        var options = ValidOptions();
        options.Secret = secret;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Jwt__Secret", result.FailureMessage);
    }

    [Fact]
    public void Validate_WithoutIssuerOrAudience_Fails()
    {
        var options = ValidOptions();
        options.Issuer = "";
        options.Audience = " ";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Jwt:Issuer", result.FailureMessage);
        Assert.Contains("Jwt:Audience", result.FailureMessage);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(-5, 7)]
    [InlineData(15, 0)]
    [InlineData(15, -1)]
    public void Validate_WithNonPositiveLifetimes_Fails(int accessMinutes, int refreshDays)
    {
        var options = ValidOptions();
        options.AccessTokenMinutes = accessMinutes;
        options.RefreshTokenDays = refreshDays;

        Assert.True(_validator.Validate(null, options).Failed);
    }
}
