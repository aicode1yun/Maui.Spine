using System.Text.Json.Serialization;

namespace MauiSpineSampleApp.Widgets.Hockey;

public enum GamePhase { Scheduled, Live, Intermission, Final }

/// <summary>A goal at <paramref name="Second"/> of <paramref name="Period"/>, and the score it made.</summary>
public sealed record Goal(int Period, int Second, string TeamId, string Scorer, int Home, int Away);

/// <summary>A finished game of the followed team.</summary>
public sealed record Result(string HomeId, string AwayId, int HomeGoals, int AwayGoals);

/// <summary>
/// The demo game both the widget and the Live Activity are drawn from. The app changes it, saves it,
/// and hands the new state to both; the widget's provider reads it back, even in a process the
/// platform started for a button tap.
/// </summary>
public sealed record Game
{
    public const int PeriodSeconds = 20 * 60;
    public const int Overtime = 4;

    public int Fixture { get; init; }
    public GamePhase Phase { get; init; }
    public int Period { get; init; }
    public DateTimeOffset FaceOffAt { get; init; }
    public DateTimeOffset? PeriodStartedAt { get; init; }
    public Goal[] Goals { get; init; } = [];
    public int HomeShots { get; init; }
    public int AwayShots { get; init; }

    /// <summary>The followed team's latest results, newest first.</summary>
    public Result[] Recent { get; init; } = [];

    // The followed team plays each opponent in turn, home and away alternately.
    private static readonly Team[] Opponents = [Teams.Foxes, Teams.Pike, Teams.Bears];

    [JsonIgnore] public Team Home => Fixture % 2 == 0 ? Teams.Followed : Opponents[Fixture % Opponents.Length];
    [JsonIgnore] public Team Away => Fixture % 2 == 0 ? Opponents[Fixture % Opponents.Length] : Teams.Followed;
    [JsonIgnore] public int HomeGoals => Goals.Count(g => g.TeamId == Home.Id);
    [JsonIgnore] public int AwayGoals => Goals.Count(g => g.TeamId == Away.Id);
    [JsonIgnore] public string Score => $"{HomeGoals}–{AwayGoals}";
    [JsonIgnore] public bool IsHome => Home == Teams.Followed;
    [JsonIgnore] public Team Opponent => IsHome ? Away : Home;
    [JsonIgnore] public bool IsRunning => Phase == GamePhase.Live;

    /// <summary>The demo's clock runs ten times as fast, so a period takes two minutes to play.</summary>
    private const int Speed = 10;

    /// <summary>The seconds played in the period; it stops at 19:59 until the period is ended.</summary>
    public int Second(DateTimeOffset now) =>
        PeriodStartedAt is { } started ? (int)Math.Clamp((now - started).TotalSeconds * Speed, 0, PeriodSeconds - 1) : 0;

    public string Clock(DateTimeOffset now) => Format(Second(now));

    public static string Format(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

    /// <summary>"P2 · 07:19", "Intermission after P1", "Final", "Face-off 19:00".</summary>
    public string PhaseText(DateTimeOffset now) => Phase switch
    {
        GamePhase.Live => $"P{Period} · {Clock(now)}",
        GamePhase.Intermission => $"Intermission after P{Period}",
        GamePhase.Final => Period == Overtime ? "Final · OT" : "Final",
        _ => $"Face-off {FaceOffAt.ToLocalTime():HH:mm}",
    };

    public static string PeriodName(int period) => period == Overtime ? "OT" : $"P{period}";

    /// <summary>"1–(2) Novak 05:35": the brackets mark the side that scored.</summary>
    public static string GoalLine(Goal goal, Team home) => goal.TeamId == home.Id
        ? $"({goal.Home})–{goal.Away} {goal.Scorer} {Format(goal.Second)}"
        : $"{goal.Home}–({goal.Away}) {goal.Scorer} {Format(goal.Second)}";

    public IEnumerable<string> LatestGoals(int count) => Phase == GamePhase.Final
        ? []
        : Goals.Reverse().Take(count).Select(goal => GoalLine(goal, Home));

    [JsonIgnore] public string Shots => $"Shots {HomeShots}–{AwayShots}";

    /// <summary>"Today 19:00", "Tomorrow 19:00", "Fri 19:00".</summary>
    public string When(DateTimeOffset now) => $"{Day(now)} {FaceOffAt.ToLocalTime():HH:mm}";

    /// <summary>"Today", "Tomorrow", "Fri".</summary>
    public string Day(DateTimeOffset now)
    {
        var local = FaceOffAt.ToLocalTime();
        return (local.Date - now.ToLocalTime().Date).Days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            _ => $"{local:ddd}",
        };
    }

    /// <summary>"W 4–2 IWB" from the followed team's side: its goals first.</summary>
    public static string ResultShort(Result result)
    {
        var home = result.HomeId == Teams.Followed.Id;
        var (ours, theirs) = home ? (result.HomeGoals, result.AwayGoals) : (result.AwayGoals, result.HomeGoals);
        var opponent = Teams.Get(home ? result.AwayId : result.HomeId);
        return $"{(ours > theirs ? "W" : "L")} {ours}–{theirs} {opponent.Abbreviation}";
    }

    public static bool Won(Result result) => result.HomeId == Teams.Followed.Id
        ? result.HomeGoals > result.AwayGoals
        : result.AwayGoals > result.HomeGoals;

    /// <summary>The first game after an install: the next fixture tonight, and two results to show.</summary>
    public static Game First(DateTimeOffset now) => new()
    {
        Fixture = 0,
        Phase = GamePhase.Scheduled,
        FaceOffAt = Evening(now),
        Recent = [new(Teams.Bears.Id, Teams.Owls.Id, 2, 4), new(Teams.Owls.Id, Teams.Pike.Id, 1, 3), new(Teams.Foxes.Id, Teams.Owls.Id, 2, 3)],
    };

    /// <summary>19:00 today, or tomorrow once today's has passed.</summary>
    public static DateTimeOffset Evening(DateTimeOffset now)
    {
        var local = now.ToLocalTime();
        var evening = new DateTimeOffset(local.Year, local.Month, local.Day, 19, 0, 0, local.Offset);
        return evening > now ? evening : evening.AddDays(1);
    }
}

[JsonSerializable(typeof(Game))]
internal sealed partial class GameJson : JsonSerializerContext;
