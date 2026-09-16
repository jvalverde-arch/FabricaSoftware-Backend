using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.SingleOrDefault(token => token.TokenHash == tokenHash));

    public void Add(RefreshToken token) => Tokens.Add(token);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(token => token.FamilyId == familyId))
        {
            token.Revoke(now);
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(token => token.UserId == userId))
        {
            token.Revoke(now);
        }

        return Task.CompletedTask;
    }
}
