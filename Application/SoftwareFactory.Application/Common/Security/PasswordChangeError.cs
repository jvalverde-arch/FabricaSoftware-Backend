namespace SoftwareFactory.Application.Common.Security;

/// <summary>Why a password change was refused; the host maps each value to a user-facing message.</summary>
public enum PasswordChangeError
{
    IncorrectCurrentPassword,
    TooShort,
    TooCommon,
}
