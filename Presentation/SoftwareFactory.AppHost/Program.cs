var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("0.8.6-pg17")
    .WithDataVolume("softwarefactory-postgres-data");

var database = postgres.AddDatabase("softwarefactory");

// Secrets are generated once and persisted in the AppHost user-secrets; nothing is committed.
var dbAppPassword = builder.AddParameter(
    "db-app-password",
    new GenerateParameterDefault { MinLength = 32, Special = false },
    secret: true,
    persist: true);
var seedAdminPassword = builder.AddParameter(
    "seed-admin-password",
    new GenerateParameterDefault { MinLength = 16, Special = false },
    secret: true,
    persist: true);

var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    new GenerateParameterDefault { MinLength = 48, Special = false },
    secret: true,
    persist: true);

var minioRootUser = builder.AddParameter("minio-root-user", "minioadmin");
var minioRootPassword = builder.AddParameter(
    "minio-root-password",
    new GenerateParameterDefault { MinLength = 24, Special = false },
    secret: true,
    persist: true);

var minio = builder.AddContainer("minio", "minio/minio", "RELEASE.2025-09-07T16-13-09Z")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", minioRootUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioRootPassword)
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithHttpHealthCheck("/minio/health/live", endpointName: "api")
    .WithVolume("softwarefactory-minio-data", "/data");

// The Api receives the owner connection (Development only: migrate, provision the app role, seed) and derives the
// RLS-enforced application connection from it with the generated role password.
builder.AddProject<Projects.SoftwareFactory_Api>("api", launchProfileName: "https")
    .WithReference(database, connectionName: "softwarefactory-admin")
    .WithEnvironment("Database__AppRolePassword", dbAppPassword)
    .WithEnvironment("Seed__AdminPassword", seedAdminPassword)
    .WithEnvironment("Jwt__SigningKeys__0__Secret", jwtSigningKey)
    .WaitFor(database)
    .WaitFor(minio)
    .WithHttpHealthCheck("/health", endpointName: "https")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.SoftwareFactory_AgentRuntime>("agentruntime")
    .WaitFor(database)
    .WaitFor(minio)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
