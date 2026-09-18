using Microsoft.Extensions.Options;
using SoftwareFactory.Application.Common.Tenancy;
using SoftwareFactory.Application.Traceability.Contracts;
using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Application.Traceability;

/// <summary>
/// The project tree (HU-004). Which mode answers a request is decided here and travels back in the response, so the
/// client never has to guess whether there is more to fetch: in lazy mode it gets one node's children and keeps
/// expanding; in filtered mode it gets the collapsed branches, already whole.
/// </summary>
public sealed class ProjectTreeService(
    IProjectTreeRepository tree,
    ITenantContext tenantContext,
    IOptions<ProjectTreeOptions> options) : IProjectTreeService
{
    private const char PathSeparator = '/';

    public async Task<ProjectTreeDto> GetAsync(ProjectTreeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        RequireTenant();

        var limits = options.Value;
        var path = ParsePath(query.Node);
        GuardLevel(query, path);

        return query.Filter.IsEmpty
            ? await LazyAsync(query, path, limits, cancellationToken).ConfigureAwait(false)
            : await FilteredAsync(query, limits, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProjectTreeDto> LazyAsync(
        ProjectTreeQuery query,
        List<Guid> path,
        ProjectTreeOptions limits,
        CancellationToken cancellationToken)
    {
        var request = new TreeChildrenQuery(query.ProjectId, path);

        if (path.Count >= limits.MaxDepth)
        {
            // The branch reached the ceiling (hierarchy rule 5). An empty answer is the honest one: there may be more
            // below, but this tree does not go there.
            return Page(query.Node, path.Count, [], matchCount: 0);
        }

        if (path.Count == 0)
        {
            return Page(query.Node, 0, await RootAsync(request, cancellationToken).ConfigureAwait(false), matchCount: 0);
        }

        var children = path[^1] == TreeNodeKinds.UnclassifiedId
            ? await tree.GetUnclassifiedAsync(request, cancellationToken).ConfigureAwait(false)
            : await tree.GetChildrenAsync(request, cancellationToken).ConfigureAwait(false);

        return Page(query.Node, path.Count, [.. children.Select(Map)], matchCount: 0);
    }

    /// <summary>
    /// First level: the project's modules (hierarchy rule 1) and, when it holds anything, the synthetic bucket of
    /// rule 3. The bucket comes last because it is where what nobody classified ends up, not a peer of the modules.
    /// </summary>
    private async Task<IReadOnlyList<ProjectTreeNodeDto>> RootAsync(TreeChildrenQuery request, CancellationToken cancellationToken)
    {
        var modules = await tree.GetRootAsync(request, cancellationToken).ConfigureAwait(false);
        var unclassified = await tree.CountUnclassifiedAsync(request, cancellationToken).ConfigureAwait(false);

        List<ProjectTreeNodeDto> nodes = [.. modules.Select(Map)];

        if (unclassified > 0)
        {
            nodes.Add(new ProjectTreeNodeDto(
                Render([TreeNodeKinds.UnclassifiedId]),
                TreeNodeKinds.UnclassifiedId,
                TreeNodeKinds.Unclassified,
                Type: null,
                Title: null,
                State: null,
                Score: null,
                EdgeType: null,
                Depth: 1,
                ChildCount: unclassified,
                Matches: false));
        }

        return nodes;
    }

    private async Task<ProjectTreeDto> FilteredAsync(ProjectTreeQuery query, ProjectTreeOptions limits, CancellationToken cancellationToken)
    {
        var request = new TreeFilterQuery(query.ProjectId)
        {
            Type = Trimmed(query.Filter.Type),
            State = ArtifactWireNames.StateOrNull(Trimmed(query.Filter.State)),
            Text = Trimmed(query.Filter.Text),
            MaxDepth = limits.MaxDepth,
            MaxMatches = limits.MaxMatches,
        };

        var nodes = await tree.GetFilteredAsync(request, cancellationToken).ConfigureAwait(false);
        var matchCount = await tree.CountMatchesAsync(request, cancellationToken).ConfigureAwait(false);

        // Expanded: there is nothing left to ask for. The cut is on the matches, so the branches that came back are
        // whole and the client can render them without another round trip.
        return new ProjectTreeDto(
            query.Node ?? string.Empty,
            0,
            Expanded: true,
            [.. nodes.Select(Map)],
            matchCount,
            Truncated: matchCount > limits.MaxMatches);
    }

    private static ProjectTreeDto Page(string? node, int level, IReadOnlyList<ProjectTreeNodeDto> nodes, int matchCount) =>
        new(node ?? string.Empty, level, Expanded: false, nodes, matchCount, Truncated: false);

    private static ProjectTreeNodeDto Map(TreeRow row) => new(
        Render(row.Path),
        row.Id,
        row.Id == TreeNodeKinds.UnclassifiedId ? TreeNodeKinds.Unclassified : TreeNodeKinds.Artifact,
        row.Type,
        row.Title,
        row.State is { } state ? ArtifactWireNames.Of(state) : null,
        row.Score,
        row.EdgeType,
        row.Path.Count,
        row.ChildCount,
        row.Matches);

    private static string Render(IReadOnlyList<Guid> path) => string.Join(PathSeparator, path);

    /// <summary>
    /// A route is ids and only ids (hierarchy rule 4). Anything else is a caller that built one by hand, and the
    /// refusal says so instead of quietly answering for the root.
    /// </summary>
    private static List<Guid> ParsePath(string? node)
    {
        if (string.IsNullOrWhiteSpace(node))
        {
            return [];
        }

        List<Guid> path = [];

        foreach (var segment in node.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Guid.TryParse(segment, out var id))
            {
                throw new ArtifactValidationException(
                    "tree",
                    [new SchemaValidationError("node", $"'{segment}' is not an identifier: a route is made of ids joined by '/'.")]);
            }

            path.Add(id);
        }

        return path;
    }

    private static void GuardLevel(ProjectTreeQuery query, List<Guid> path)
    {
        if (query.Level != 0 && query.Level != path.Count)
        {
            throw new ArtifactValidationException(
                "tree",
                [new SchemaValidationError("level", $"The level {query.Level} does not match the route, which has {path.Count} step(s).")]);
        }
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new InvalidOperationException("The project tree needs a tenant in context.");
}
