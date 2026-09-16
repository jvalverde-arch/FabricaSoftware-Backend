using SoftwareFactory.Application.Platform.Contracts;

namespace SoftwareFactory.Application.Tests.Platform.Fakes;

internal sealed class FakeJobHandler(string jobType) : IJobHandler
{
    public string JobType { get; } = jobType;

    public List<JobExecution> Executions { get; } = [];

    public Func<JobExecution, Task>? OnHandle { get; set; }

    public Task HandleAsync(JobExecution execution, CancellationToken cancellationToken)
    {
        Executions.Add(execution);
        return OnHandle?.Invoke(execution) ?? Task.CompletedTask;
    }
}
