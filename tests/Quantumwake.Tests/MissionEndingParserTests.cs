using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;

namespace Quantumwake.Tests;

/// <summary>
/// Reading the two lines that say a contract is over.
/// </summary>
/// <remarks>
/// Every line below is copied out of this install's backups rather than
/// written to fit the regex. Both tags had been in the logs the whole time and
/// nothing in the app mentioned either of them.
/// </remarks>
public class MissionEndingParserTests
{
    private static MissionEndedEvent Parse(string raw)
    {
        Assert.True(LogEnvelope.TryParse(raw, out var line));

        return Assert.IsType<MissionEndedEvent>(new LogEventParser().Parse(line));
    }

    [Fact]
    public void The_push_message_gives_the_mission_and_that_it_completed()
    {
        var ended = Parse(
            "<2026-05-03T18:06:45.316Z> [Notice] <MissionEnded> Received MissionEnded " +
            "push message for: mission_id 0f5d986f-7b2d-41ff-9d20-61f6eb573e06 - " +
            "mission_state MISSION_STATE_COMPLETED [Team_GameServices][Missions]");

        Assert.Equal("0f5d986f-7b2d-41ff-9d20-61f6eb573e06", ended.MissionId);
        Assert.Equal(MissionEnding.Completed, ended.Ending);
        Assert.Null(ended.Reason);
    }

    [Theory]
    [InlineData("MISSION_STATE_COMPLETED", MissionEnding.Completed)]
    [InlineData("MISSION_STATE_WITHDRAWN", MissionEnding.Abandoned)]
    [InlineData("MISSION_STATE_FAILED", MissionEnding.Failed)]
    public void Every_state_this_install_has_seen_is_understood(string state, MissionEnding expected)
    {
        var ended = Parse(
            "<2026-05-03T18:06:45.316Z> [Notice] <MissionEnded> Received MissionEnded " +
            "push message for: mission_id 0f5d986f-7b2d-41ff-9d20-61f6eb573e06 - " +
            $"mission_state {state} [Team_GameServices][Missions]");

        Assert.Equal(expected, ended.Ending);
    }

    /// <summary>
    /// A state the game has not shown us yet is unknown rather than a guess.
    /// Reading it as completion would bank a contract as done on the strength
    /// of a word nobody has seen.
    /// </summary>
    [Fact]
    public void A_state_nobody_has_seen_is_not_read_as_completion()
    {
        var ended = Parse(
            "<2026-05-03T18:06:45.316Z> [Notice] <MissionEnded> Received MissionEnded " +
            "push message for: mission_id 0f5d986f-7b2d-41ff-9d20-61f6eb573e06 - " +
            "mission_state MISSION_STATE_SOMETHING_NEW [Team_GameServices][Missions]");

        Assert.Equal(MissionEnding.Unknown, ended.Ending);
    }

    [Fact]
    public void The_other_tag_gives_the_reason_as_well()
    {
        var ended = Parse(
            "<2026-05-02T02:39:45.501Z> [Notice] <EndMission> Ending mission for player. " +
            "MissionId[cbb50710-4a8b-43a9-89b1-8a570393d7bb] Player[nekron] " +
            "PlayerId[9730519752057] CompletionType[Abandon] Reason[Player left] " +
            "[Team_MissionFeatures][Missions]");

        Assert.Equal("cbb50710-4a8b-43a9-89b1-8a570393d7bb", ended.MissionId);
        Assert.Equal(MissionEnding.Abandoned, ended.Ending);
        Assert.Equal("Player left", ended.Reason);
    }

    [Theory]
    [InlineData("Complete", MissionEnding.Completed)]
    [InlineData("Abandon", MissionEnding.Abandoned)]
    [InlineData("Fail", MissionEnding.Failed)]
    public void Every_completion_type_this_install_has_seen_is_understood(
        string type, MissionEnding expected)
    {
        var ended = Parse(
            "<2026-05-02T02:39:45.501Z> [Notice] <EndMission> Ending mission for player. " +
            "MissionId[cbb50710-4a8b-43a9-89b1-8a570393d7bb] Player[nekron] " +
            $"PlayerId[9730519752057] CompletionType[{type}] Reason[Player left] " +
            "[Team_MissionFeatures][Missions]");

        Assert.Equal(expected, ended.Ending);
    }

    /// <summary>
    /// "Deactivate" is one ending in 300 and is not something the player did.
    /// Filing it as an abandonment would put a contract they never dropped in
    /// front of them as one they did.
    /// </summary>
    [Fact]
    public void A_deactivated_mission_is_not_called_an_abandonment()
    {
        var ended = Parse(
            "<2026-05-02T02:39:45.501Z> [Notice] <EndMission> Ending mission for player. " +
            "MissionId[cbb50710-4a8b-43a9-89b1-8a570393d7bb] Player[nekron] " +
            "PlayerId[9730519752057] CompletionType[Deactivate] Reason[Player left] " +
            "[Team_MissionFeatures][Missions]");

        Assert.Equal(MissionEnding.Unknown, ended.Ending);
    }
}
