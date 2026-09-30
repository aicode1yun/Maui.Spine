using Plugin.Maui.Spine.Core;
using Plugin.Maui.Spine.Svg;

namespace Plugin.Maui.Spine.Scanner;

public static class ScannerExtensions
{
    /// <summary>
    /// Registers <see cref="BarcodeScannerView"/>'s camera handlers and the ready-made <see cref="BarcodeScannerPage"/>.
    /// <c>UseSpine()</c> calls it for an app that references this package; an app without Spine calls it itself
    /// and can use the view but not the page. Calling it more than once is harmless.
    /// </summary>
    public static MauiAppBuilder UseSpineScanner(this MauiAppBuilder builder)
    {
        if (builder.Services.Any(static s => s.ServiceType == typeof(Registered)))
            return builder;
        builder.Services.AddSingleton<Registered>();

#if IOS || MACCATALYST || ANDROID
        builder.ConfigureMauiHandlers(static handlers => handlers.AddHandler<BarcodeScannerView, BarcodeScannerViewHandler>());
#endif

        // The page lives in this package: Spine has already scanned the app's assemblies by now, but its
        // navigation registry is built on first use, so adding the assembly here is still in time
        var assembly = typeof(ScannerExtensions).Assembly;
        if (builder.Services.FirstOrDefault(static s => s.ServiceType == typeof(SpineOptions))?.ImplementationInstance is SpineOptions options
            && !options.Assemblies.Contains(assembly))
        {
            options.AddAssembly(assembly);
            builder.Services.AddTransient<BarcodeScannerPage>();
            builder.Services.AddTransient<BarcodeScannerPageViewModel>();
        }
        builder.UseEmbeddedSvgImages(assembly);

        ScannerStrings.EnsureRegistered();
        return builder;
    }

    private sealed class Registered;
}
