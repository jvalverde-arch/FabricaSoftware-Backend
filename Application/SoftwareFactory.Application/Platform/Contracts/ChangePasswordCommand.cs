namespace SoftwareFactory.Application.Platform.Contracts;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);
