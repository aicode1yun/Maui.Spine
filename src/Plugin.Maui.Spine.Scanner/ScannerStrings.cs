using Plugin.Maui.Spine.Common;

namespace Plugin.Maui.Spine.Scanner;

/// <summary>
/// Registers the package's default text (keys <c>Spine.Scanner.*</c>) the first time a scanner is used,
/// so the text is there even for an app that does not call <see cref="ScannerExtensions.UseSpineScanner"/>.
/// </summary>
internal static class ScannerStrings
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
            SpineStrings.Current.AddDefaults(new EmbeddedXmlStringProvider(typeof(ScannerStrings).Assembly));
    }

    public static string Get(string key)
    {
        EnsureRegistered();
        return SpineStrings.Current[$"Spine.Scanner.{key}"];
    }

    public static string For(ScannerProblem problem) => Get(problem.ToString());
}
