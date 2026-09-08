using Quantumwake.Core.Events;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// When a contract is over, as opposed to when a step of it is.
/// </summary>
/// <remarks>
/// <para>
/// These exist because the app used to answer that question with the first
/// objective to complete, and a hauling run has a pickup before it has a
/// dropoff - so a contract finished the moment the cargo was loaded. It looked
/// right on every single-objective contract, which is most of them, and wrong
/// by a median of 521 seconds on the 96 of 212 missions in this install that
/// complete more than one. The worst was early by 3 hours 25 minutes.
/// </para>
/// <para>
/// The game says it plainly, twice, and neither tag was being read.
/// </para>
/// </remarks>
public class MissionEndingTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 5, 3, 18, 0, 0, TimeSpan.Zero);

    private const string Mission = "0f5d986f-7b2d-41ff-9d20-61f6eb573e06";
    private const string Raw = "Ling_Stanton_VeryEasy_RecoverCargo";

    private static SessionSummary Built(params GameEvent[] events)
    {
        var builder = new SessionBuilder("Game Build(1) 03 May 26 (13 41 25).log");
        foreach (var e in events) builder.Add(e);
        return builder.Build();
    }

    private static ContractEvent Marker(DateTimeOffset at) =>
        new(at, Mission, null, Raw, null);

    private static MissionObjectiveEvent Step(
        DateTimeOffset at, string id, ObjectiveState state) =>
        new(at, Mission, id, state, ShownInLog: true);

    private static MissionEndedEvent Ended(
        DateTimeOffset at, MissionEnding ending, string? reason = null) =>
        new(at, Mission, ending, reason);

    private static ContractRecord Only(SessionSummary session) =>
        Assert.Single(session.Contracts, c => c.MissionId == Mission);

    /// <summary>
    /// The bug, stated directly: a two-step contract with one step done is not
    /// a finished contract.
    /// </summary>
    [Fact]
    public void Finishing_the_pickup_does_not_finish_the_contract()
    {
        var contract = Only(Built(
            Marker(T0),
            Step(T0.AddSeconds(10), "pickup_0", ObjectiveState.InProgress),
            Step(T0.AddSeconds(20), "dropoff_0", ObjectiveState.InProgress),
            Step(T0.AddSeconds(30), "pickup_0", ObjectiveState.Completed)));

        Assert.Equal(ContractOutcome.InProgress, contract.Outcome);
        Assert.Null(contract.CompletedAt);
    }

    /// <summary>
    /// Even every known step finishing is not the signal. Objectives are added
    /// as a contract unfolds, so "all of them" only ever means "all of them so
    /// far" - which is why counting them cannot answer this and the game's own
    /// ending has to.
    /// </summary>
    [Fact]
    public void Even_every_step_so_far_finishing_is_not_the_signal()
    {
        var contract = Only(Built(
            Marker(T0),
            Step(T0.AddSeconds(10), "pickup_0", ObjectiveState.Completed),
            Step(T0.AddSeconds(20), "dropoff_0", ObjectiveState.Completed)));

        Assert.Equal(ContractOutcome.InProgress, contract.Outcome);
        Assert.Equal(2, contract.Steps);
        Assert.Equal(2, contract.StepsDone);
    }

    [Fact]
    public void The_ending_completes_it_and_dates_it()
    {
        var end = T0.AddMinutes(9);

        var contract = Only(Built(
            Marker(T0),
            Step(T0.AddSeconds(30), "pickup_0", ObjectiveState.Completed),
            Ended(end, MissionEnding.Completed)));

        Assert.Equal(ContractOutcome.Completed, contract.Outcome);
        Assert.Equal(end, contract.CompletedAt);
    }

    /// <summary>
    /// Both tags fire for every completion in this install - 231 missions, 462
    /// events - and they land milliseconds apart. The second must not move the
    /// time the first recorded.
    /// </summary>
    [Fact]
    public void The_second_tag_does_not_move_the_time_the_first_recorded()
    {
        var first = T0.AddMinutes(9);

        var contract = Only(Built(
            Marker(T0),
            Ended(first, MissionEnding.Completed),
            Ended(first.AddMilliseconds(180), MissionEnding.Completed)));

        Assert.Equal(first, contract.CompletedAt);
    }

    /// <summary>
    /// The server keeps upserting objectives while it tears a finished mission
    /// down, and none of that may reopen a contract that is over.
    /// </summary>
    [Fact]
    public void Objectives_arriving_after_the_ending_do_not_reopen_it()
    {
        var end = T0.AddMinutes(9);

        var contract = Only(Built(
            Marker(T0),
            Ended(end, MissionEnding.Completed),
            Step(end.AddSeconds(1), "cleanup_0", ObjectiveState.InProgress)));

        Assert.Equal(ContractOutcome.Completed, contract.Outcome);
        Assert.Equal(end, contract.CompletedAt);
    }

    [Fact]
    public void Walking_away_is_an_abandonment_with_no_completion_date()
    {
        var contract = Only(Built(
            Marker(T0),
            Ended(T0.AddMinutes(4), MissionEnding.Abandoned, "Player left")));

        Assert.Equal(ContractOutcome.Abandoned, contract.Outcome);
        Assert.Null(contract.CompletedAt);
    }

    /// <summary>
    /// Failure is filed as abandonment for now - the rest of the app has no
    /// word for it yet - but it must never be filed as done.
    /// </summary>
    [Fact]
    public void A_failed_contract_is_not_a_completed_one()
    {
        var contract = Only(Built(
            Marker(T0),
            Ended(T0.AddMinutes(4), MissionEnding.Failed)));

        Assert.NotEqual(ContractOutcome.Completed, contract.Outcome);
        Assert.Null(contract.CompletedAt);
    }

    /// <summary>
    /// One ending in 300 is a "Deactivate" that the player did not cause. An
    /// ending nobody has words for must not bank a contract as done, and must
    /// not stand in the way of a real ending beside it.
    /// </summary>
    [Fact]
    public void An_ending_nobody_has_words_for_settles_nothing()
    {
        var contract = Only(Built(
            Marker(T0),
            Ended(T0.AddMinutes(4), MissionEnding.Unknown),
            Ended(T0.AddMinutes(5), MissionEnding.Completed)));

        Assert.Equal(ContractOutcome.Completed, contract.Outcome);
        Assert.Equal(T0.AddMinutes(5), contract.CompletedAt);
    }

    /// <summary>
    /// The marker and the push messages are separate streams, so a contract
    /// can be introduced after it has already ended. It is still over.
    /// </summary>
    [Fact]
    public void An_ending_before_the_marker_still_ends_it()
    {
        var end = T0.AddMinutes(9);

        var contract = Only(Built(
            Step(T0.AddSeconds(30), "pickup_0", ObjectiveState.Completed),
            Ended(end, MissionEnding.Completed),
            Marker(end.AddSeconds(2))));

        Assert.Equal(ContractOutcome.Completed, contract.Outcome);
        Assert.Equal(end, contract.CompletedAt);
    }

    /// <summary>
    /// The same bug in its other hiding place: a marker arriving after the
    /// first step finished used to make the contract born complete.
    /// </summary>
    [Fact]
    public void A_marker_arriving_after_a_finished_step_is_not_born_complete()
    {
        var contract = Only(Built(
            Step(T0.AddSeconds(30), "pickup_0", ObjectiveState.Completed),
            Marker(T0.AddSeconds(40))));

        Assert.Equal(ContractOutcome.InProgress, contract.Outcome);
        Assert.Null(contract.CompletedAt);
    }
}
