using SoftwareFactory.Application.Traceability;
using SoftwareFactory.Infrastructure.Traceability;

namespace SoftwareFactory.Infrastructure.Tests.Traceability;

/// <summary>
/// Validation of content per artifact type against the real schemas of the catalog (HU-001 §1 and its tests):
/// a valid document passes, and a broken one comes back with the field that is wrong so the Api can answer 422.
/// </summary>
public sealed class ArtifactSchemaValidationTests
{
    private static readonly ArtifactSchemaRegistry _registry = ArtifactSchemaRegistry.Embedded;
    private static readonly NJsonSchemaValidator _validator = new();

    public static TheoryData<string, string> ValidContent => new()
    {
        { "module", """{"name":"Cobros","purpose":"Gestiona la cobranza","owner_role":"functional"}""" },
        { "user_story", """{"as_a":"cajera","i_want":"registrar un pago","so_that":"la deuda baje","acceptance_criteria":["el saldo disminuye"]}""" },
        { "functional_requirement", """{"statement":"El sistema registra el pago","priority":"must"}""" },
        { "non_functional_requirement", """{"category":"performance","statement":"Responde en menos de 2 s","metric":"p95 < 2000 ms"}""" },
        { "business_rule", """{"statement":"Un pago no puede exceder la deuda","applies_to":["pago"]}""" },
        { "use_case", """{"actor":"cajera","main_flow":["abre la caja","registra el pago"],"postconditions":["saldo actualizado"]}""" },
        { "screen", """{"purpose":"Registrar pagos","elements":["buscador","tabla"]}""" },
        { "actor", """{"description":"Persona que cobra en ventanilla","permissions":["registrar pago"]}""" },
        { "api", """{"method":"POST","path":"/api/payments","errors":["409 saldo insuficiente"]}""" },
        { "data_entity", """{"description":"Pago recibido","attributes":[{"name":"amount","type":"decimal","required":true}],"identifier":"id"}""" },
        { "architecture_component", """{"responsibility":"Procesa pagos","technology":".NET 10"}""" },
        { "adr", """{"context":"Necesitamos cola","decision":"Postgres","consequences":"Una pieza menos que operar"}""" },
        { "compliance_requirement", """{"regulation":"Ley 1581","statement":"Consentimiento explícito","jurisdiction":"Colombia"}""" },
        { "test_case", """{"kind":"e2e","steps":["abrir caja","registrar pago"],"expected_result":"el saldo baja"}""" },
        { "boundary_contract", """{"contract_class":"api","identifier":"payments.v1","contract_version":"1.0.0","signature":{"method":"POST"},"lifecycle":"active"}""" },
    };

    [Theory]
    [MemberData(nameof(ValidContent))]
    public void Valid_content_of_every_type_of_the_catalog_passes(string type, string content)
    {
        var schema = _registry.GetCurrent(type);

        var errors = _validator.Validate(schema.Json, content);

        Assert.Empty(errors);
    }

    [Fact]
    public void Every_type_of_the_catalog_has_a_valid_sample_in_this_suite()
    {
        var covered = ValidContent.Select(row => (string)row[0]!).Order();

        Assert.Equal(_registry.Types.Order(), covered);
    }

    [Fact]
    public void A_missing_required_field_names_that_field()
    {
        var schema = _registry.GetCurrent("user_story");

        var errors = _validator.Validate(schema.Json, """{"as_a":"cajera","i_want":"registrar un pago"}""");

        Assert.Contains(errors, error => error.Path.Contains("soThat", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Path.Contains("acceptanceCriteria", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_field_outside_the_schema_is_rejected()
    {
        var schema = _registry.GetCurrent("screen");

        var errors = _validator.Validate(schema.Json, """{"purpose":"Registrar","color":"azul"}""");

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.Path.Contains("color", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_value_outside_the_enumeration_is_rejected_with_its_path()
    {
        var schema = _registry.GetCurrent("test_case");

        var errors = _validator.Validate(schema.Json, """{"kind":"smoke","steps":["x"],"expected_result":"y"}""");

        Assert.Contains(errors, error => error.Path == "kind");
    }

    [Fact]
    public void An_error_deep_in_an_array_carries_its_index()
    {
        var schema = _registry.GetCurrent("data_entity");

        var errors = _validator.Validate(
            schema.Json,
            """{"description":"Pago","attributes":[{"name":"amount"}]}""");

        Assert.Contains(errors, error => error.Path.StartsWith("attributes[0]", StringComparison.Ordinal));
    }
}
