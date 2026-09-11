namespace SoftwareFactory.Api.Contracts.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
