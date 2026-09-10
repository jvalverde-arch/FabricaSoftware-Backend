var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("0.8.6-pg17")
    .WithDataVolume("softwarefactory-postgres-data");

var database = postgres.AddDatabase("softwarefactory");

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

builder.AddProject<Projects.SoftwareFactory_Api>("api", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(minio)
    .WithHttpHealthCheck("/health", endpointName: "https")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.SoftwareFactory_AgentRuntime>("agentruntime")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(minio)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
