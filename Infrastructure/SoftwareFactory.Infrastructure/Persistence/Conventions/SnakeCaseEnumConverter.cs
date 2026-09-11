using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>Stores enums as snake_case text (draft, in_review, ...) so the column is readable and check-constrained (estandar-backend.md §4).</summary>
internal sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => EnumText<TEnum>.ToText(value),
    text => EnumText<TEnum>.FromText(text))
    where TEnum : struct, Enum;
