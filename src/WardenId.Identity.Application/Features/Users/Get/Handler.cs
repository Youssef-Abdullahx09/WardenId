using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.Get;

public sealed class Handler(IIdentityDbContext dbContext) : IRequestHandler<Query, Result<UserDetail>>
{
    public async Task<Result<UserDetail>> Handle(Query request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.Attributes)
            .Include(u => u.MfaFactors)
            .Where(u => u.Id == request.UserId && u.TenantId == request.TenantId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) return Result.Fail(UserErrors.NotFound);

        var detail = new UserDetail(
            user.Id,
            user.Email.Value,
            user.Username.Value,
            user.Status,
            user.Lockout.FailedAttemptCount,
            user.Lockout.LockedUntilUtc,
            user.PendingActions.ToArray(),
            user.Attributes.Select(a => new UserAttributeSummary(a.Key, a.Value, a.ValueType)).ToArray(),
            user.MfaFactors.Select(f => new MfaFactorSummary(f.Id, f.FactorType, f.Enabled)).ToArray(),
            user.CreatedAt);

        return Result.Ok(detail);
    }
}
