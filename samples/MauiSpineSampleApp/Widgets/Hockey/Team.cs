namespace MauiSpineSampleApp.Widgets.Hockey;

/// <summary>
/// A made-up club with a badge of the sample's own (<c>Resources/Svg/Team*.svg</c>), so the live score
/// shows no real team, player or logo.
/// </summary>
public sealed record Team(string Id, string Name, string ShortName, string Abbreviation, string[] Roster)
{
    public string LogoSvg => $"Team{Id}.svg";

    /// <summary>The badge in its own colours, stored for the widget and the Live Activity.</summary>
    public string LogoAsset => $"team-{Id.ToLowerInvariant()}.png";

    /// <summary>The badge on a filled circle, for Android's Live Update, which crops its icon to one.</summary>
    public string BadgeAsset => $"team-{Id.ToLowerInvariant()}-badge.png";
}

public static class Teams
{
    public static readonly Team Owls = new("Owls", "Northport Owls", "Owls", "NPO", ["Carter", "Lindqvist", "Okafor", "Moreau", "Sato"]);
    public static readonly Team Foxes = new("Foxes", "Ridgeview Foxes", "Foxes", "RVF", ["Novak", "Bennett", "Haraldsen", "Diallo", "Park"]);
    public static readonly Team Pike = new("Pike", "Lakeside Pike", "Pike", "LSP", ["Ferreira", "Kowalski", "Brandt", "Ito", "Mensah"]);
    public static readonly Team Bears = new("Bears", "Ironwood Bears", "Bears", "IWB", ["Walsh", "Eriksen", "Rossi", "Kaur", "Dubois"]);

    public static IReadOnlyList<Team> All { get; } = [Owls, Foxes, Pike, Bears];

    /// <summary>The club the widget follows; every game is one of theirs.</summary>
    public static Team Followed => Owls;

    public static Team Get(string id) => All.First(t => t.Id == id);
}
