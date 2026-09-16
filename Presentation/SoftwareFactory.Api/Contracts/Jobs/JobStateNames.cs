using System.Collections.Frozen;
using SoftwareFactory.Domain.Platform;

namespace SoftwareFactory.Api.Contracts.Jobs;

/// <summary>Wire names of the job states: snake_case, the same the database column stores.</summary>
public static class JobStateNames
{
    private static readonly FrozenDictionary<JobState, string> _names = new Dictionary<JobState, string>
    {
        [JobState.Pending] = "pending",
        [JobState.Running] = "running",
        [JobState.Succeeded] = "succeeded",
        [JobState.Failed] = "failed",
        [JobState.Cancelled] = "cancelled",
    }.ToFrozenDictionary();

    public static string Of(JobState state) => _names[state];
}
