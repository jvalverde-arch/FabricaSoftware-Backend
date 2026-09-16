using SoftwareFactory.Domain.Finops;

namespace SoftwareFactory.Application.Tests.Finops.Fakes;

internal sealed class FakeLlmCallRepository : ILlmCallRepository
{
    public List<LlmCall> Calls { get; } = [];

    public void Add(LlmCall llmCall) => Calls.Add(llmCall);

    public Task<decimal> GetJobCostAsync(Guid jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Calls.Where(call => call.JobId == jobId).Sum(call => call.Cost));
}
