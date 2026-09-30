using Plugin.Maui.Spine.Common;
using SkiaSharp;
using Svg.Skia;

namespace MauiSpineSampleApp.Widgets.Hockey;

/// <summary>
/// The badges as PNGs in the widget store, where the widget and the Live Activity find them by name.
/// Drawn once per app build from the embedded SVGs the app itself shows them with.
/// </summary>
public static class TeamLogos
{
    /// <summary>Big enough for 44 points at 3×, small enough for the extension's 30 MB.</summary>
    private const int Size = 144;

    /// <summary>The widget's surface, behind the badge that Android crops to a circle.</summary>
    private static readonly SKColor Circle = new(0x1C, 0x22, 0x2D);

    private const string StoredKey = "live-score-logos";

    public static async Task EnsureAsync(IWidgetService widgets, CancellationToken cancellationToken = default)
    {
        var build = AppInfo.Current.BuildString;
        if (!widgets.IsSupported || Preferences.Default.Get<string?>(StoredKey, null) == build) return;

        foreach (var team in Teams.All)
        {
            using (var png = new MemoryStream(Render(team.LogoSvg)))
                await widgets.StoreAssetAsync(team.LogoAsset, png, cancellationToken);

            using (var badge = new MemoryStream(Render(team.LogoSvg, Circle)))
                await widgets.StoreAssetAsync(team.BadgeAsset, badge, cancellationToken);
        }

        Preferences.Default.Set(StoredKey, build);
    }

    /// <summary>
    /// The SVG centred on a square PNG in its own colours; with a <paramref name="circle"/>, at two thirds
    /// of the width on that circle, so a host that crops to a circle cuts nothing off.
    /// </summary>
    private static byte[] Render(string svgFile, SKColor? circle = null)
    {
        using var stream = typeof(TeamLogos).Assembly.GetManifestResourceStream($"MauiBottomSheetPoc.Resources.Svg.{svgFile}")
            ?? throw new InvalidOperationException($"{svgFile} is not embedded.");
        using var svg = new SKSvg();
        var picture = svg.Load(stream) ?? throw new InvalidOperationException($"{svgFile} did not parse.");

        var bounds = picture.CullRect;
        var scale = (circle is null ? Size : Size * 0.66f) / Math.Max(bounds.Width, bounds.Height);

        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (circle is { } fill)
        {
            using var paint = new SKPaint { Color = fill, IsAntialias = true };
            canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f, paint);
        }
        canvas.Translate((Size - bounds.Width * scale) / 2, (Size - bounds.Height * scale) / 2);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
