namespace SoftwareFactory.Domain.Common;

/// <summary>Project roles shared by authorization policies and the decision log (estandar-auth.md §4).</summary>
public enum Role
{
    Admin,
    Functional,
    Architect,
    Qa,
    Compliance,
    Reader,
}
