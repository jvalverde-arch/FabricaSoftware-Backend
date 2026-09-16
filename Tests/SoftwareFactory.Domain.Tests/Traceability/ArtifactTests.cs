using SoftwareFactory.Domain.Traceability;

namespace SoftwareFactory.Domain.Tests.Traceability;

/// <summary>State machine and invariants of an artifact (HU-001): draft → in_review → approved → frozen.</summary>
public sealed class ArtifactTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_artifact_is_a_draft_without_content_or_score()
    {
        var artifact = NewArtifact();

        Assert.Equal(ArtifactState.Draft, artifact.State);
        Assert.Equal(0, artifact.CurrentVersion);
        Assert.Null(artifact.Score);
        Assert.False(artifact.IsDeleted);
    }

    [Theory]
    [InlineData(ArtifactState.Draft, ArtifactState.InReview)]
    [InlineData(ArtifactState.InReview, ArtifactState.Approved)]
    [InlineData(ArtifactState.InReview, ArtifactState.Draft)]
    [InlineData(ArtifactState.Approved, ArtifactState.Frozen)]
    [InlineData(ArtifactState.Approved, ArtifactState.Draft)]
    public void The_allowed_transitions_move_the_artifact(ArtifactState from, ArtifactState to)
    {
        var artifact = InState(from);

        artifact.ChangeState(to, _now);

        Assert.Equal(to, artifact.State);
        Assert.Equal(_now, artifact.UpdatedAt);
    }

    [Theory]
    [InlineData(ArtifactState.Draft, ArtifactState.Approved)]
    [InlineData(ArtifactState.Draft, ArtifactState.Frozen)]
    [InlineData(ArtifactState.InReview, ArtifactState.Frozen)]
    [InlineData(ArtifactState.Frozen, ArtifactState.Draft)]
    [InlineData(ArtifactState.Frozen, ArtifactState.Approved)]
    public void Any_other_transition_is_refused(ArtifactState from, ArtifactState to)
    {
        var artifact = InState(from);

        var exception = Assert.Throws<InvalidOperationException>(() => artifact.ChangeState(to, _now));

        Assert.Contains(from.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(from, artifact.State);
    }

    [Fact]
    public void Moving_to_the_same_state_changes_nothing_and_is_not_an_error()
    {
        var artifact = InState(ArtifactState.InReview);

        artifact.ChangeState(ArtifactState.InReview, _now);

        Assert.Equal(ArtifactState.InReview, artifact.State);
    }

    [Fact]
    public void A_frozen_artifact_accepts_no_new_content()
    {
        var artifact = InState(ArtifactState.Frozen);

        Assert.Throws<InvalidOperationException>(() => artifact.AdvanceVersion(_now));
        Assert.Throws<InvalidOperationException>(() => artifact.Rename("otro título", _now));
    }

    [Fact]
    public void Writing_content_advances_the_version_number()
    {
        var artifact = NewArtifact();

        Assert.Equal(1, artifact.AdvanceVersion(_now));
        Assert.Equal(2, artifact.AdvanceVersion(_now));
        Assert.Equal(2, artifact.CurrentVersion);
    }

    [Fact]
    public void The_title_can_be_changed_while_the_artifact_is_editable()
    {
        var artifact = NewArtifact();

        artifact.Rename("Como usuaria quiero entrar", _now);

        Assert.Equal("Como usuaria quiero entrar", artifact.Title);
        Assert.Equal(_now, artifact.UpdatedAt);
    }

    [Fact]
    public void The_score_stays_inside_the_scale_and_may_be_cleared()
    {
        var artifact = NewArtifact();

        artifact.SetScore(85, _now);
        Assert.Equal(85, artifact.Score);

        artifact.SetScore(null, _now);
        Assert.Null(artifact.Score);

        Assert.Throws<ArgumentOutOfRangeException>(() => artifact.SetScore(101, _now));
        Assert.Throws<ArgumentOutOfRangeException>(() => artifact.SetScore(-1, _now));
    }

    [Fact]
    public void Deleting_is_logical_and_idempotent()
    {
        var artifact = NewArtifact();

        artifact.Delete(_now);

        Assert.True(artifact.IsDeleted);
        Assert.Equal(_now, artifact.DeletedAt);

        artifact.Delete(_now.AddDays(1));
        Assert.Equal(_now, artifact.DeletedAt);
    }

    [Fact]
    public void A_deleted_artifact_is_not_edited_any_more()
    {
        var artifact = NewArtifact();
        artifact.Delete(_now);

        Assert.Throws<InvalidOperationException>(() => artifact.AdvanceVersion(_now));
        Assert.Throws<InvalidOperationException>(() => artifact.Rename("x", _now));
        Assert.Throws<InvalidOperationException>(() => artifact.ChangeState(ArtifactState.InReview, _now));
    }

    private static Artifact NewArtifact() =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), "user_story", "Como usuaria quiero iniciar sesión", ArtifactLevel.Project);

    private static Artifact InState(ArtifactState state)
    {
        var artifact = NewArtifact();

        foreach (var step in PathTo(state))
        {
            artifact.ChangeState(step, _now);
        }

        return artifact;
    }

    private static IEnumerable<ArtifactState> PathTo(ArtifactState state) => state switch
    {
        ArtifactState.Draft => [],
        ArtifactState.InReview => [ArtifactState.InReview],
        ArtifactState.Approved => [ArtifactState.InReview, ArtifactState.Approved],
        ArtifactState.Frozen => [ArtifactState.InReview, ArtifactState.Approved, ArtifactState.Frozen],
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
