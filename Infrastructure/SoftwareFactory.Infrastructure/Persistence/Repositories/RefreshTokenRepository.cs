using Microsoft.EntityFrameworkCore;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Infrastructure.Persistence.Repositories;

/// <summary>Revocations mark tracked entities so the unit of work commits them with the rest of the operation.</summary>
public sealed class RefreshTokenRepository(SoftwareFactoryDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => context.RefreshTokens.Add(token);

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        RevokeAsync(context.RefreshTokens.Where(token => token.FamilyId == familyId), now, cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        RevokeAsync(context.RefreshTokens.Where(token => token.UserId == userId), now, cancellationToken);

    private static async Task RevokeAsync(IQueryable<RefreshToken> tokens, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var alive = await tokens.Where(token => token.RevokedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var token in alive)
        {
            token.Revoke(now);
        }
    }
}
