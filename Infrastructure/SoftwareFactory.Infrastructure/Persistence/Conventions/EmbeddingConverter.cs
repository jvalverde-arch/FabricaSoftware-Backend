using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>Maps the domain embedding (<see cref="ReadOnlyMemory{T}"/> of float) to the pgvector <c>vector</c> type.</summary>
internal sealed class EmbeddingConverter() : ValueConverter<ReadOnlyMemory<float>, Vector>(
    memory => new Vector(memory),
    vector => vector.Memory);
