using Mono.Cecil;
using NetArchTest.Rules;

namespace SoftwareFactory.Api.Tests.Architecture;

/// <summary>
/// Custom NetArchTest rule: a type of one functional module may only reach another module through that module's
/// public contracts namespace. Every type reference (signatures, fields, attributes and method bodies) is inspected.
/// </summary>
internal sealed class ModuleIsolationRule : ICustomRule
{
    private readonly IReadOnlyList<string> _forbiddenNamespaces;
    private readonly IReadOnlyList<string> _allowedNamespaces;
    private readonly List<string> _violations = [];

    private ModuleIsolationRule(IReadOnlyList<string> forbiddenNamespaces, IReadOnlyList<string> allowedNamespaces)
    {
        _forbiddenNamespaces = forbiddenNamespaces;
        _allowedNamespaces = allowedNamespaces;
    }

    /// <summary>Distinct violations seen so far; NetArchTest may evaluate a rule more than once per type.</summary>
    public IReadOnlyList<string> Violations => [.. _violations.Distinct(StringComparer.Ordinal)];

    /// <summary>Domain modules never reference each other: Domain has no contracts namespace.</summary>
    public static ModuleIsolationRule ForDomainModule(string module)
    {
        var otherModules = OtherModules(module);

        return new ModuleIsolationRule(
            forbiddenNamespaces: [.. otherModules.Select(other => $"{SolutionLayout.Domain}.{other}")],
            allowedNamespaces: []);
    }

    /// <summary>Application modules reach other modules only through <c>SoftwareFactory.Application.{Other}.Contracts</c>.</summary>
    public static ModuleIsolationRule ForApplicationModule(string module)
    {
        var otherModules = OtherModules(module);

        return new ModuleIsolationRule(
            forbiddenNamespaces:
            [
                .. otherModules.Select(other => $"{SolutionLayout.Application}.{other}"),
                .. otherModules.Select(other => $"{SolutionLayout.Domain}.{other}"),
            ],
            allowedNamespaces:
            [
                .. otherModules.Select(other => $"{SolutionLayout.Application}.{other}.{SolutionLayout.ContractsSegment}"),
            ]);
    }

    public bool MeetsRule(TypeDefinition type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var offending = ReferencedTypes(type)
            .SelectMany(Expand)
            .Select(RootNamespaceOf)
            .Where(IsForbidden)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var ns in offending)
        {
            _violations.Add($"{type.FullName} -> {ns}");
        }

        return offending.Count == 0;
    }

    private static IEnumerable<string> OtherModules(string module) =>
        SolutionLayout.Modules.Where(candidate => !string.Equals(candidate, module, StringComparison.Ordinal));

    private bool IsForbidden(string ns) =>
        _forbiddenNamespaces.Any(forbidden => SolutionLayout.IsWithin(ns, forbidden))
        && !_allowedNamespaces.Any(allowed => SolutionLayout.IsWithin(ns, allowed));

    private static string RootNamespaceOf(TypeReference type)
    {
        var declaring = type;

        while (declaring.DeclaringType is not null)
        {
            declaring = declaring.DeclaringType;
        }

        return declaring.Namespace;
    }

    private static IEnumerable<TypeReference> Expand(TypeReference type)
    {
        yield return type;

        if (type is GenericInstanceType generic)
        {
            foreach (var argument in generic.GenericArguments.SelectMany(Expand))
            {
                yield return argument;
            }
        }

        if (type is TypeSpecification specification)
        {
            foreach (var element in Expand(specification.ElementType))
            {
                yield return element;
            }
        }
    }

    private static IEnumerable<TypeReference> ReferencedTypes(TypeDefinition type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (var implementation in type.Interfaces)
        {
            yield return implementation.InterfaceType;
        }

        foreach (var attribute in type.CustomAttributes)
        {
            yield return attribute.AttributeType;
        }

        foreach (var constraint in type.GenericParameters.SelectMany(parameter => parameter.Constraints))
        {
            yield return constraint.ConstraintType;
        }

        foreach (var field in type.Fields)
        {
            yield return field.FieldType;
        }

        foreach (var property in type.Properties)
        {
            yield return property.PropertyType;
        }

        foreach (var eventDefinition in type.Events)
        {
            yield return eventDefinition.EventType;
        }

        foreach (var reference in type.Methods.SelectMany(ReferencedTypes))
        {
            yield return reference;
        }

        foreach (var reference in type.NestedTypes.SelectMany(ReferencedTypes))
        {
            yield return reference;
        }
    }

    private static IEnumerable<TypeReference> ReferencedTypes(MethodDefinition method)
    {
        yield return method.ReturnType;

        foreach (var parameter in method.Parameters)
        {
            yield return parameter.ParameterType;
        }

        foreach (var attribute in method.CustomAttributes)
        {
            yield return attribute.AttributeType;
        }

        foreach (var constraint in method.GenericParameters.SelectMany(parameter => parameter.Constraints))
        {
            yield return constraint.ConstraintType;
        }

        if (!method.HasBody)
        {
            yield break;
        }

        foreach (var variable in method.Body.Variables)
        {
            yield return variable.VariableType;
        }

        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.Operand)
            {
                case TypeReference typeReference:
                    yield return typeReference;
                    break;

                case MethodReference methodReference:
                    yield return methodReference.DeclaringType;
                    yield return methodReference.ReturnType;

                    foreach (var parameter in methodReference.Parameters)
                    {
                        yield return parameter.ParameterType;
                    }

                    break;

                case FieldReference fieldReference:
                    yield return fieldReference.DeclaringType;
                    yield return fieldReference.FieldType;
                    break;

                default:
                    break;
            }
        }
    }
}
