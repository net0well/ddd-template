using Application.Abstractions.Messaging;

namespace Application.Users.PurgeExpiredRefreshTokens;

/// <summary>Deletes expired refresh tokens. Run by a recurring job; returns how many were deleted.</summary>
public sealed record PurgeExpiredRefreshTokensCommand : ICommand<int>;
