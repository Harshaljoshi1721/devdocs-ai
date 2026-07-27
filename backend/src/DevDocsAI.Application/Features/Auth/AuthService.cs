using DevDocsAI.Application.Abstractions;
using DevDocsAI.Application.Abstractions.Persistence;
using DevDocsAI.Application.Abstractions.Security;
using DevDocsAI.Application.Common.Exceptions;
using DevDocsAI.Application.Common.Validation;
using DevDocsAI.Domain.Entities;
using FluentValidation;

namespace DevDocsAI.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string rawRefreshToken, CancellationToken ct);
    Task LogoutAsync(string rawRefreshToken, CancellationToken ct);
}

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IClock clock,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAppAsync(request, ct);

        var email = Normalize(request.Email);
        if (await users.EmailExistsAsync(email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User(email, request.Name.Trim(), passwordHasher.Hash(request.Password));
        await users.AddAsync(user, ct);
        var result = await IssueTokensAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAppAsync(request, ct);

        var user = await users.GetByEmailAsync(Normalize(request.Email), ct);
        // Same error whether the user is missing or the password is wrong (no account enumeration).
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationException("Invalid email or password.");
        }

        var result = await IssueTokensAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            throw new AuthenticationException("Missing refresh token.");
        }

        var existing = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken), ct);
        if (existing is null || !existing.IsActive(clock.UtcNow))
        {
            throw new AuthenticationException("Invalid or expired refresh token.");
        }

        var user = await users.GetByIdAsync(existing.UserId, ct)
            ?? throw new AuthenticationException("Invalid or expired refresh token.");

        // Rotate: revoke the presented token and issue a fresh pair.
        existing.Revoke(clock.UtcNow);
        var result = await IssueTokensAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return; // idempotent
        }

        var existing = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken), ct);
        if (existing is not null)
        {
            existing.Revoke(clock.UtcNow);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResult> IssueTokensAsync(User user, CancellationToken ct)
    {
        var access = tokenService.CreateAccessToken(user);
        var refresh = tokenService.CreateRefreshToken();
        await refreshTokens.AddAsync(new RefreshToken(user.Id, refresh.Hash, refresh.ExpiresAt), ct);

        return new AuthResult(
            access.Value,
            access.ExpiresAt,
            refresh.RawValue,
            refresh.ExpiresAt,
            new UserDto(user.Id, user.Email, user.Name));
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
