# Spine.Images — bildcache, förhämtning, nedskalning och blurhash (förstudie, rev 1)

**Status:** Förstudie. Inget implementerat. Issue: [#305](https://github.com/jonatansoderberg/Maui.Spine/issues/305), prioriterad som P2 i [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317). Studien är gjord i en Linux-container utan Mac och utan enhet: inget i den är byggt eller kört. Den bygger på källkod (Spine, MAUI `main`, Nuke 13.2.0, Glide 4.16.0), paketinnehåll från nuget.org och dokumentation.
**Fråga:** #317 ställer två frågor. Hur ska Nuke bindas? Kan widget-extensionen dela bildcachen med appen? Bakom dem finns en tredje fråga: vad saknas egentligen i MAUI, plattform för plattform?
**Svar:** Nuke ska inte bindas i v1, och cachen ska inte delas. Det som saknas på iOS är ett minnesdiskcache, avkodning i visningsstorlek och avkodning utanför huvudtråden. Allt det finns i Microsoft.iOS (`NSCache`, ImageIO:s `CGImageSource.CreateThumbnail`) och kan skrivas i C#. Samma C#-kärna (diskcache, sammanslagning av samtidiga hämtningar, förhämtning) behövs ändå på Windows, där MAUI inte cachar alls. Android har redan Glide via MAUI och behöver bara förhämtning och rensning. Widget-extensionen får en **kopia** av bilden, nedskalad och skriven till den assetbutik den redan läser (`IWidgetService.StoreAssetAsync`), och ingen delad cache. Om mätningar på enhet visar att C#-vägen inte räcker blir Nuke plan B. Den ska då byggas som en egen Swift-brygga med Nukes källor, kompilerade med `swiftc` vid appbygget på samma sätt som Widgets gör. Den ska aldrig byggas via `ImageCaching.Nuke`.

---

## 1. Slutsatsen i korthet

| Fråga | Svar | Belägg |
|---|---|---|
| Vad saknas i MAUI på iOS? | **Minnescache, nedskalning och cachegiltighet.** `UriImageSourceService` på iOS sparar hela filen i `Caches/com.microsoft.maui/MauiUriImages` och räknar den som cachad så länge filen finns: `CacheValidity` läses aldrig. Varje visning avkodar hela bilden med `CGImageSource.CreateImage(0)` i skala 1. | MAUI `main`: `UriImageSourceService.iOS.cs:46–66, 104–107`, `ImageSourceExtensions.cs:147–172` (iOS) |
| På Android? | **Nästan inget.** MAUI laddar URI:er med Glide (4.16, `Xamarin.Android.Glide` 4.16.0.14) och får minnes- och diskcache (250 MB). Glide mäter dessutom `ImageView` och avkodar i vyns storlek. Det som saknas är förhämtning och rensning. | `PlatformInterop.java:308–355`, Glide `DiskCache.java:14`, `CustomViewTarget.java:438–460` |
| På Windows? | **Allt.** MAUI:s `UriImageSource` skapar en ny `HttpClient` per laddning, och cachegrenen är bortkommenterad (`// TODO: CACHING`). Bilden avkodas i full storlek i en `BitmapImage`. | MAUI `main`: `Controls/src/Core/UriImageSource.cs:89–122`, `UriImageSourceService.Windows.cs` |
| Ska Spine binda Nuke? | **Inte i v1.** C#-vägen med ImageIO och `NSCache` löser det issuen beskriver och kan delas med Windows. Nuke tillför progressiv avkodning, prioritering och ett moget LRU-diskcache. Det kostar ett Swift-bygge av 55 källfiler och en brygga med callbacks, och hjälper bara iOS. | §4 |
| Om Nuke ändå behövs, hur? | **Egen `@objc`-brygga och Nuke 13.2.0 från källkod, kompilerade med `swiftc` vid appbygget** (samma mönster som `SpineWidgetBridge`). `ImageCaching.Nuke` 5.0.0 är ingen utväg: dess xcframework saknar Mac Catalyst-slice, och proxyns API saknar nedskalning och val av cachekatalog. Ett förbyggt xcframework går inte att bygga i CI, som kör på `windows-latest`. | §4, `unzip -l imagecaching.nuke.5.0.0.nupkg`, `.github/workflows/ci.yml:13` |
| Kan widget-extensionen dela cachen? | **Nej. Den får en kopia.** Widgetrenderaren läser bara lagrade assets (`UIImage(contentsOfFile:)` under `spine-widgets/assets/`) och har inga URL-bilder. En bildcache är LRU-styrd och kan rensas av systemet, men en widgetbild får inte försvinna. Nuke avråder själv från två `DataCache`-instanser på samma katalog, och två processer ger just det. | `SpineWidgetRenderer.swift:43–45`, `WidgetPlatform.Apple.cs:138–144`, Nuke `DataCache.swift:34–35` |
| Blurhash eller ThumbHash? | **Båda går att avkoda i ren C#** utan beroenden. Båda algoritmerna är korta, och MIT-licensierade portar finns (`Blurhash.Core` 4.2.1, `ThumbHash` 2.1.1). Vilken som ska vara primär är ägarens beslut (§3.4). | nuspec på nuget.org |

---

## 2. Vad som redan finns

### 2.1 MAUI:s bildladdning per plattform

Källan är `dotnet/maui` `main` (2026-09-28). Repot pinnar `Microsoft.Maui.Controls` 10.0.50 (`Directory.Packages.props:10`), men skillnaden mot 10.0.50 är inte kontrollerad fil för fil.

| | iOS / Mac Catalyst | Android | Windows |
|---|---|---|---|
| Laddare | `UriImageSourceService.iOS.cs`, egen kod | `PlatformInterop.loadImageFromUri` → Glide | `UriImageSourceService.Windows.cs` → `BitmapImage.SetSourceAsync` |
| Minnescache | Ingen | Glides (`LruResourceCache`) | Ingen |
| Diskcache | Hela filen, CRC64 av URL:en som namn, ingen utgång (`IsImageCached` = `File.Exists`) | Glides, 250 MB i `image_manager_disk_cache` | Ingen |
| `CachingEnabled=false` | Hoppar över filen | `DiskCacheStrategy.NONE` + `skipMemoryCache` | Ingen skillnad |
| `CacheValidity` | Läses inte | Läses inte | Läses inte |
| Nedskalning | Nej, full avkodning i skala 1 | Ja, till vyns storlek. Vid `wrap_content` används skärmens största mått. | Nej (men `DecodePixelWidth` finns, §3.3) |
| Animerad GIF | Ja (`ImageAnimationHelper`) | Ja (Glide) | Ja (WinUI) |
| Utbytbar | Ja: `ConfigureImageSources` + `AddService<UriImageSource, …>`. En konkret typ vinner över MAUI:s gränssnittsregistrering (`ImageSourceToImageSourceServiceTypeMapping.FindImageSourceServiceType`). | Samma, men Glides request byggs i Java och går inte att ändra utifrån | Samma |

Glide-konfigurationen går inte att nå så som issuen antar. MAUI äger appens enda `AppGlideModule` (`MauiGlideModule`), och den sätter bara loggnivå i `applyOptions`. En `LibraryGlideModule` från Spine kan registrera komponenter men inte ändra `GlideBuilder` (diskcachens storlek, minnesstorlek). Den enda vägen dit är `Glide.init(Context, GlideBuilder)` före första laddningen. Metoden är publik men märkt `@VisibleForTesting` i 4.16.0, så den ska inte användas i v1.

### 2.2 I Spine

| Del | Var | Vad det betyder för bilder |
|---|---|---|
| Egen `IImageSourceService` | `Svg/MauiAppBuilderExtensions.cs:46–47`, `SvgBitmapImageSourceService.Apple.cs:12,31`, `SvgBitmapImageSourceService.Android.cs:13,33` | Mönstret finns och fungerar redan: en tjänst per plattform, registrerad med `ConfigureImageSources`, som avkodar med rätt skala. En tjänst för `UriImageSource` är samma form. |
| Attached properties + mappning | `SvgImageSource.cs:20` (statisk klass, `CreateAttached` från rad 26), `Extensions/Material.cs:97`, `Extensions/GlassExtensions.cs:53` | `ImageOptions.BlurHash` / `.Downsample` / `.FadeIn` följer samma form som `Material.Kind` och `Glass.Style`. |
| Rendering i vyns storlek | `SvgImageSourceBehavior` (lyssnar på `SizeChanged`) | Samma sätt att få målstorleken för nedskalning. |
| Widgetbilder | `IWidgetService.StoreAssetAsync` (`IWidgetService.cs:25`), `StorePackageAssetAsync` (`:35`), `WidgetAsset.Rolling` (`WidgetAsset.cs:15`), `W.Image(assetId)` (`W.cs:54`) | Widgets visar bara bitmappar som appen lagrat under ett asset-id. iOS skriver dem till App Group-katalogen `spine-widgets/assets/` (`WidgetPlatform.Apple.cs:49,138–144`). Android skriver till `FilesDir/spine-widgets/assets` (`WidgetStore.cs:22`). |
| Widgetrenderaren | `SpineWidgetRenderer.swift:30–46` (`Store.image(asset:)`), `:434–445` (fjärrkälla) | Extensionen läser assets synkront från filen. `RemoteSource` (`WidgetTimeline.cs:151`) hämtar bara JSON-dokumentet. En backend kan inte peka ut en ny bild, bara ett asset-id som appen redan skrivit. |
| Swift i paket | `spine-widgets-build.sh:312` (`swiftc -emit-library -module-name SpineWidgetBridge`), `Plugin.Maui.Spine.Widgets.targets:128` (`NativeReference`), `WidgetPlatform.Apple.cs:22,230` (`objc_msgSend`, ingen bindning) | Så här skulle en Nuke-brygga byggas (§4). Issuen skriver "xcframework in `native/`", men Widgets och Push skickar Swift-**källor** som kompileras vid appbygget. De skickar inga förbyggda binärer. |
| Push-bilder | `SpinePushNotificationService.swift:35` (`URLSession.downloadTask`) | Notification Service Extension laddar ner sin bild själv. Den bör inte heller dela cache. Det är samma resonemang som för widgets. |
| Egen nedladdning | `HeroCollectionView.AdaptiveOverlay.cs:217–220` (`new HttpClient()` för `UriImageSource`) | Laddar bilden en gång till för färganalysen. Den ska gå via `IImageCache` när den finns (§5). |
| Skelett | `Plugin.Maui.Spine.Controls.Shimmer` (`Skeleton.cs`, `SkeletonDrawable.cs`) | Det grå alternativet till blurhash. De konkurrerar inte: skelettet gäller sidan medan data laddas, blurhash gäller en bild vars URL redan är känd. |
| Servern | `Plugin.Maui.Spine.Server` (`net10.0`, bara push: `Azure.Data.Tables`, `FirebaseAdmin`) | Har ingen bildavkodare. En blurhash-hjälpare där skulle dra in SkiaSharp eller ImageSharp (§3.4). |
| Minimiversioner | `Directory.Build.props:22–25`: iOS/Catalyst 15.0, Android 21, Windows 10.0.17763 | Nuke 13.2.0 kräver iOS 15 och passar. Nukes `main` kräver iOS 16. |

---

## 3. Plattformarnas byggstenar

### 3.1 iOS och Mac Catalyst

**I Microsoft.iOS, utan Swift:**
- `CGImageSource.CreateThumbnail(0, new CGImageThumbnailOptions { MaxPixelSize = …, CreateThumbnailFromImageAlways = true, ShouldCacheImmediately = true })` avkodar direkt till målstorleken utan att hela bitmappen passerar minnet. `ShouldCacheImmediately` tvingar fram avkodningen på den tråd som anropar, så den kan läggas utanför huvudtråden. Det här är ImageIO:s väg. Nuke använder samma väg för `ImageRequest.thumbnail`.
- `NSCache` med `TotalCostLimit` och kostnad = bytes i bitmappen. Cachen tömmer sig själv vid minnesvarning, och bitmapparna hålls på den nativa sidan, så .NET:s GC behöver inte se dem.
- `NSUrlSession` eller `HttpClient` (som i .NET for iOS går genom `NSUrlSessionHandler`) för hämtningen, och filer under `FileSystem.CacheDirectory`.

**Nuke 13.2.0** (MIT, senaste release, iOS 15, `swift-tools-version:6.0`, 55 Swift-filer i `Sources/Nuke`):
`ImagePipeline` med minnescache (`ImageCache`), LRU-diskcache (`DataCache`, 150 MB som standard, `init(path:)` för valfri katalog), `ImagePrefetcher`, `ImageProcessors.Resize`, `ImageRequest.ThumbnailOptions`, `isProgressiveDecodingEnabled`, sammanslagning av likadana requests och prioritering. Övergångar och platshållare för `UIImageView` finns i målet `NukeExtensions`.

**Befintliga .NET-paket:**

| Paket | Senaste | Mål | Vad det är |
|---|---|---|---|
| `ImageCaching.Nuke` | 5.0.0 (2025-11-13) | `net9.0-ios18.0`, `net9.0-maccatalyst18.0`, `net10.0-ios26.0`, `net10.0-maccatalyst26.0` | NukeProxy: ett förbyggt `NukeProxy.xcframework` med Nuke inbyggt, bundet med en tunn `@objc`-yta. Paketet saknar licensuttryck. |
| `Sharpnado.Maui.Nuke` | 12.8.3 (2025-11-22) | Bara `net9.0-*` (MAUI 9.0.82) | Ersätter MAUI:s bildtjänster på iOS med ovanstående. MIT. |

Två saker i `ImageCaching.Nuke` 5.0.0 avgör frågan. De är kontrollerade genom att packa upp paketet:
1. **Ingen Mac Catalyst-slice.** Både `lib/net10.0-maccatalyst26.0/…resources.zip` och `net9.0-maccatalyst18.0` innehåller bara `ios-arm64` och `ios-arm64_x86_64-simulator`. Det borde ge länkfel på Catalyst. Det är inte provat.
2. **Proxyns API** (`NukeProxy.swiftinterface`): `ImagePipeline.setupWithDataCache()`, `loadImage(url:onCompleted:)`, `loadImage(url:placeholder:errorImage:into:)`, `isCached`, `removeAllCaches`, `Prefetcher(destination:maxConcurrentRequestCount:)`. Det finns ingen storlek, ingen processor, ingen thumbnail och ingen väg att välja cachekatalog. Nedskalning och en egen katalog går alltså inte att få via paketet.

### 3.2 Android

Glide 4.16 finns i varje MAUI-app, och C#-bindningen `Bumptech.Glide` kommer transitivt via `Xamarin.Android.Glide`:
- Förhämtning: `Glide.With(context).Load(uri).Preload()`. Den laddar i originalstorlek till minne och disk. `DiskCacheStrategy.AUTOMATIC` sparar fjärrdata som original (DATA), så en senare laddning i vyns storlek träffar diskcachen. Det är inte provat om cachenyckeln blir densamma när MAUI laddar med `android.net.Uri` och Spine förhämtar med en sträng. Det ska verifieras.
- Rensning: `Glide.Get(context).ClearMemory()` på huvudtråden och `ClearDiskCache()` på en bakgrundstråd.
- Platshållare och tona in: MAUI:s request byggs i Java utan `placeholder`/`transition`. `MauiCustomViewTarget` överskuggar inte `onResourceLoading`, så en drawable som satts på `ImageView` före laddningen bör ligga kvar tills `onResourceReady` ersätter den. Det är läst ur källan och inte kört.

Coil är inte aktuellt. Det vore en andra bildladdare bredvid MAUI:s Glide.

### 3.3 Windows

`BitmapImage.DecodePixelWidth`/`DecodePixelHeight` (med `DecodePixelType.Logical`) avkodar i målstorlek. Issuen säger "no native downsampling", men det stämmer inte. Cache, sammanslagning och förhämtning måste skrivas i C#, och det är samma kod som iOS-vägen i §4 D. Det är inte kontrollerat om en `BitmapImage` kan delas mellan flera `Image`-element, vilket en minnescache skulle behöva.

### 3.4 Blurhash och ThumbHash

| | BlurHash | ThumbHash |
|---|---|---|
| Form | Base83-sträng, 20–30 tecken | Bytes (~25), i praktiken base64 |
| Enligt upphovet | Konfigurerbart antal komponenter (4×3 vanligt) | "Encodes more detail in the same space", sidförhållande, alfa, inga parametrar |
| Spridning | Stor: bland annat Unsplash och Mastodon levererar färdiga hashar | Mindre |
| .NET | `Blurhash.Core` 4.2.1 (MIT, netstandard2.0, `Blurhasher.Encode/Decode` på `Pixel[,]`). `Blurhash.SkiaSharp` 2.0.0 drar in SkiaSharp **2.88**, medan Spine kör 3.119. | `ThumbHash` 2.1.1 (MIT, net6.0/netstandard2.0, `ThumbHash.FromImage`, `ToImage()` → RGBA) |

Rekommendationen är egen avkodning i paketet: en fil per algoritm, RGBA till en 32×32-bitmapp som skalas upp mjukt av plattformen. Algoritmerna är korta, och ett beroende för hundra rader är inte värt det. Det ska inte gå via PNG, för en platshållare ska synas i samma layoutpass (jfr `SvgBitmapImageSourceService.Android.cs`, som avkodar synkront av just det skälet, #345). Avkodningen bör ta under en millisekund. Det är inte mätt.

Kodningen hör inte hemma i appen. Servern behöver en bildavkodare för att kunna räkna fram hashen. Förslag: en `BlurHash.Encode(ReadOnlySpan<byte> rgba, int width, int height)` i `Plugin.Maui.Spine.Common` (`net10.0`, inga beroenden). Servern avkodar uppladdningen med det bibliotek den redan har och anropar den. Serverpaketet får då inget bildbibliotek.

---

## 4. Alternativen för iOS

| | A. `ImageCaching.Nuke` / Sharpnado | B. Egen brygga + Nuke från källkod | C. Egen brygga + förbyggt xcframework | D. C#-kärna + ImageIO + `NSCache` |
|---|---|---|---|---|
| Minnes- och diskcache | Ja | Ja | Ja | Ja (egen LRU för disk) |
| Nedskalning | **Nej** (inte i proxyn) | Ja (`thumbnail`) | Ja | Ja (`CreateThumbnail`) |
| Progressiv JPEG | Nej i proxyn | Ja | Ja | Nej |
| Mac Catalyst | **Ingen slice** | Ja, `spine-widgets-build.sh` bygger redan `-macabi` | Kräver egen slice | Ja |
| Bygge | NuGet | `swiftc` av ~55 filer + brygga per RID vid appbygget. Tiden är inte mätt, men widgetbygget tar ~10 s för 5 filer. | Kräver Mac för att producera paketet. CI kör `windows-latest` (`ci.yml:13`, `release.yml:18`). | Inget |
| Brygga till C# | Färdig | `@objc` + `objc_msgSend`, men laddning är asynkron och kräver block-callbacks (`BlockLiteral`). Det är inte prövat i repot. | Samma | Ingen |
| Windows | Hjälper inte | Hjälper inte | Hjälper inte | **Samma kärna** (disk, sammanslagning, förhämtning). Bara avkodningen skiljer. |
| Binärstorlek | NukeProxy ios-arm64: 717 kB | Liknande | Liknande | ~0 |
| Underhåll | Två externa underhållare, fyra TFM:er efter | Nukes uppgraderingar som källkod | Samma + binärer i git | Eget |

**Rekommendation: D i v1.** Issuen klagar på tre saker: ingen minnescache, ingen nedskalning och ingen förhämtning. Alla tre löses med API:er som redan är bundna, och en del av koden (disk, sammanslagning, förhämtning) delas med Windows. Nukes verkliga fördelar är progressiv avkodning, prioritering och ett väl beprövat LRU-diskcache. Ingen av apparnas användningar behöver dem: klubbmärken, lagloggor och temabakgrunder är små eller få.

**B är plan B.** Den gäller om D på enhet inte ger jämn scrollning i en lista med ~200 bilder, jämfört med Sharpnado.Maui.Nuke i samma lista (§9 steg 1). I så fall vendoras Nuke 13.2.0 i `native/ios/Nuke/` med sin licens, byggs med bryggan till `SpineImages.framework` i samma skript och mönster som `SpineWidgetBridge`, och C# anropar en `@objc`-klass med URL, målstorlek i pixlar och en callback. A utesluts på grund av Catalyst och det saknade storleks-API:et. C utesluts på grund av CI.

---

## 5. Föreslagen API-yta

Paketet heter `Plugin.Maui.Spine.Images` och registrerar sig som en `SpineModule` (jfr `Plugin.Maui.Spine.Widgets.props:4`), så att `UseSpine()` tar med det.

```csharp
namespace Plugin.Maui.Spine.Images;

/// <summary>The app's remote-image cache. Every <see cref="UriImageSource"/> goes through it once the package is installed.</summary>
public interface IImageCache
{
    /// <summary>Downloads into the disk cache so the images show later without the network.</summary>
    Task PrefetchAsync(IEnumerable<Uri> uris, CancellationToken cancellationToken = default);

    /// <summary>Whether <paramref name="uri"/> is on disk; nothing is downloaded.</summary>
    bool Contains(Uri uri);

    /// <summary>
    /// The image at most <paramref name="maxPixelSize"/> pixels on its longest side, as PNG, from the cache
    /// or the network. For handing a picture to something outside the app's views, such as
    /// <c>IWidgetService.StoreAssetAsync</c>.
    /// </summary>
    Task<Stream> LoadPngAsync(Uri uri, int maxPixelSize, CancellationToken cancellationToken = default);

    Task ClearAsync(ImageCacheScope scope = ImageCacheScope.All);
}

[Flags]
public enum ImageCacheScope { Memory = 1, Disk = 2, All = Memory | Disk }

public sealed class SpineImagesOptions
{
    /// <summary>iOS and Windows; on Android Glide's own limit (250 MB) applies.</summary>
    public long DiskCacheSize { get; set; } = 150 * 1024 * 1024;
    public int MaxConcurrentDownloads { get; set; } = 4;
}

/// <summary>Per-image options for an <see cref="Image"/> with a remote source.</summary>
public static class ImageOptions
{
    public static readonly BindableProperty BlurHashProperty =
        BindableProperty.CreateAttached("BlurHash", typeof(string), typeof(ImageOptions), null,
            propertyChanged: static (b, _, _) => (b as Image)?.Handler?.UpdateValue(nameof(IImage.Source)));

    public static readonly BindableProperty ThumbHashProperty = /* same, string (base64) */;

    /// <summary>Decode at the view's size instead of the image's. iOS and Windows; Android always does.</summary>
    public static readonly BindableProperty DownsampleProperty = /* bool, false */;

    public static readonly BindableProperty FadeInProperty = /* bool, false */;

    public static string? GetBlurHash(BindableObject view) => (string?)view.GetValue(BlurHashProperty);
    public static void SetBlurHash(BindableObject view, string? value) => view.SetValue(BlurHashProperty, value);
    // ...
}
```

```xml
<Image Source="{Binding BadgeUrl}"
       ImageOptions.BlurHash="{Binding BadgeHash}"
       ImageOptions.Downsample="True"
       ImageOptions.FadeIn="True"
       WidthRequest="40" HeightRequest="40" />
```

**Varför ett gränssnitt och inte en statisk `ImageCache`.** Spines tjänster är injicerade (`IWidgetService`, `IPushService`, `ILocalNotificationService`). Issuens statiska skiss bryter mot det utan att vinna något.

**Varför ingen `Widgets`-referens.** Widgetfallet blir fyra rader i appen, och paketen förblir oberoende av varandra:

```csharp
foreach (var team in teams)
{
    await using var png = await _images.LoadPngAsync(team.LogoUrl, maxPixelSize: 120);
    await _widgets.StoreAssetAsync($"logos/{team.Id}.png", png);
}
await _widgets.RefreshAsync<GamesWidget>();
```

**Två lager, två mekanismer:**
1. *Tjänsten.* `AddService<UriImageSource, SpineUriImageSourceService>` på iOS och Windows ger minnes- och diskcache och avkodning utanför huvudtråden för **alla** fjärrbilder, utan ändrad markup. En animerad bild (`ImageCount > 1`) lämnas till MAUI:s egen `UriImageSourceService`, som är publik, så att GIF fortsätter fungera.
2. *Alternativen.* `ImageOptions.*` kräver att vyns storlek är känd, men `IImageSourceService.GetImageAsync` får ingen storlek. Därför blir det en `ModifyMapping` på `ImageHandler`s `Source`. När något alternativ är satt läggs platshållaren i den nativa vyn direkt. Sedan körs MAUI:s mappning, och målstorleken tas från `WidthRequest`/`HeightRequest` eller första `SizeChanged`, som i `SvgImageSourceBehavior`. En bild utan känd storlek skalas till högst skärmbredden gånger densiteten. På Android betyder `Downsample` inget, eftersom Glide redan skalar ner.

`HeroCollectionView`s egen `HttpClient`-nedladdning (`AdaptiveOverlay.cs:217`) byts mot `IImageCache` när paketet finns. Det kan göras med en valfri tjänst, så att kontrollpaketet inte beror på Images.

---

## 6. Widget-extensionen och cachen

| Väg | Utfall |
|---|---|
| Nukes eller Spines diskcache i App Group-katalogen, läst av båda processerna | **Nej.** Två cacheinstanser på samma katalog sveper och skriver oberoende av varandra. Nuke: "It's possible to have more than one instance of `DataCache` with the same path but it is not recommended." En LRU-svep kan dessutom ta bort en bild som en widget visar. |
| Extensionen länkar samma cachebibliotek | **Nej.** Det ger större appex och mer minne i en process med snäv minnesgräns. `IWidgetService` säger själv "the renderer runs under a tight memory limit". Apple anger ingen siffra. |
| **Kopia till assetbutiken** (`LoadPngAsync` → `StoreAssetAsync`) | **Ja.** Det använder vägen som redan finns och redan fungerar för Live Activities (Puckkolls lagloggor). Bilden skrivs i den storlek widgeten visar och ligger utanför `Library/Caches`, så den rensas inte. `WidgetAsset.Rolling` håller butiken begränsad för daterade bilder. Android fungerar likadant (`WidgetStore.AssetsDirectory`). |
| Fjärrdokument som pekar på bild-URL:er (`RemoteSource` utan appen) | **Senare, eget steg.** Det kräver att `SpineWidgetRenderer.swift` laddar ner bilder medan tidslinjen byggs (den hämtar redan JSON där, `:442`) till en egen katalog, till exempel `spine-widgets/remote/`, med en enkel städning mot dokumentets aktuella URL:er. Det är widgetkod och inte bildcachekod. Det görs när en app behöver det. |

Enligt Apple skapar iOS bara `Library/Caches` automatiskt i gruppkatalogen. Det är inte dokumenterat om systemet tömmer just den vid lagringsbrist. Det spelar ingen roll här, eftersom assets ligger i `spine-widgets/`.

---

## 7. Plattformar

| Plattform | Cache | Nedskalning | Förhämtning | Platshållare / tona in |
|---|---|---|---|---|
| iOS 15+ | Egen: `NSCache` + LRU-filer | ImageIO `CreateThumbnail` | `IImageCache`, egen kö | I `Source`-mappningen, `UIImageView` |
| Mac Catalyst 15+ | Samma kod | Samma | Samma | Samma. Inte verifierad. |
| Android 21+ | Glide via MAUI (oförändrad) | Glide, redan i dag | `Glide.Load(uri).Preload()` | Drawable före MAUI:s laddning. Tona in behöver en egen övergång (§8). |
| Windows | Egen: minne + LRU-filer (samma kärna som iOS) | `DecodePixelWidth` | Samma kö som iOS | `Source`-mappningen, WinUI `Image` |

Issuen kallar Windows för "Partial", men det stämmer inte längre. Windows får samma yta som iOS.

---

## 8. Vad som inte går, och vad som inte är verifierat

1. **Ingenting är kört.** Studien är gjord utan Mac, simulator eller enhet. Påståendena om MAUI, Glide och Nuke kommer från källkoden och påståendena om paketen från deras innehåll. Ingen kod är provad.
2. **D mot Nuke är inte mätt.** Rekommendationen vilar på vad issuen säger saknas, inte på en profil. Steg 1 i leveransplanen är just en mätning, och B står kvar som ett fullt beskrivet alternativ.
3. **`ImageCaching.Nuke` på Catalyst.** Att slicen saknas är kontrollerat i paketet. Att det ger länkfel är en slutsats, inte ett körresultat.
4. **Platshållare och tona in i `Source`-mappningen.** Det är okänt om MAUI:s `ImageSourcePartLoader` nollar den nativa bilden när en ny laddning börjar. Då skulle platshållaren försvinna direkt. Tona in kräver att man kommer åt ögonblicket då bilden sätts, och det äger MAUI. Båda kräver en spike per plattform. Om de inte håller skjuts `FadeIn` till v2.
5. **Glides cachenyckel** för `Uri` jämfört med sträng (§3.2) är inte verifierad. Förhämtningen ska laddas med exakt den modelltyp MAUI använder.
6. **Glides diskcachestorlek** går inte att ändra utan `@VisibleForTesting`-API:t. `DiskCacheSize` gäller därför inte Android, och det ska stå i dokumentationen.
7. **`BitmapImage` delad mellan element** på Windows är inte kontrollerat. Om det inte går blir minnescachen på Windows en cache av bytes och inte av avkodade bilder.
8. **GC och nativt minne.** `NSCache` håller `UIImage` på den nativa sidan, men varje `UIImage` som en vy visar har också en .NET-referens. Om den skapar tryck på GC:n eller läcker vid snabb scrollning syns först i Instruments.
9. **Mätningen av en bild utan känd storlek.** En `Image` som får sin storlek av bilden kan inte skalas ner till vyn. Taket i skärmstorlek är en gissning om vad som är rimligt.

---

## 9. Leveransplan

1. **Spike och mätning (iOS, enhet).** En lista med ~200 fjärrbilder i `MauiSpineSampleApp`: MAUI som den är, Sharpnado.Maui.Nuke (MAUI 9-paketet provas mot 10) och en minimal D-tjänst. Mät scrollning, minnestopp och tid till första bild. Här avgörs D eller B.
2. **Kärnan och iOS/Windows-tjänsten.** `IImageCache`, disk-LRU, sammanslagning, förhämtningskö, `SpineUriImageSourceService` för iOS, Catalyst och Windows med GIF-reserv, `SpineImagesOptions`, `UnsupportedImageCache` där inget finns.
3. **Android.** `IImageCache` ovanpå Glide: förhämtning, `Contains` (om Glide tillåter det utan att ladda), rensning och `LoadPngAsync`.
4. **`ImageOptions`.** BlurHash- och ThumbHash-avkodare med tester mot referensvektorerna från upphovsrepona, `Downsample`, platshållaren. `FadeIn` bara om spiken i §8 punkt 4 håller.
5. **Widgets.** Mönstret i §5 i `docs/wiki/widgets.md` och i samplet: `LoadPngAsync` → `StoreAssetAsync`. Ingen kod i Widgets.
6. **`BlurHash.Encode` i Common** och ett exempel i `MauiSpinePushNotificationsSampleApp.Server` som räknar fram hashen vid uppladdning.
7. **Wiki** `docs/wiki/images.md`, med gränserna i §8 punkt 5–6 och §6.
8. **Senare, vid behov:** bild-URL:er i fjärrdokument för widgets (§6 sista raden) och Nuke-vägen B om steg 1 kräver den.

**Beslut som ägaren behöver fatta:** D eller B efter steg 1. BlurHash, ThumbHash eller båda i v1. Om `Downsample` ska vara på som standard på iOS, vilket vore i linje med vad Android redan gör. Om `HeroCollectionView` ska känna till `IImageCache`.

---

## 10. Referenser

- Nuke (MIT, 13.2.0): https://github.com/kean/Nuke — `Package.swift` vid taggen `13.2.0`, `Sources/Nuke/Caching/DataCache.swift`, `Sources/Nuke/ImageRequest.swift` (`ThumbnailOptions`), `Sources/Nuke/Pipeline/ImagePipeline+Configuration.swift`
- NuGet, `ImageCaching.Nuke` 5.0.0: https://www.nuget.org/packages/ImageCaching.Nuke — källa https://github.com/roubachof/NukeProxy
- NuGet, `Sharpnado.Maui.Nuke` 12.8.3: https://www.nuget.org/packages/Sharpnado.Maui.Nuke — källa https://github.com/roubachof/Maui.Nuke
- dotnet/maui `main`: `src/Core/src/ImageSources/UriImageSourceService/UriImageSourceService.{iOS,Android,Windows}.cs`, `src/Core/src/ImageSources/iOS/ImageSourceExtensions.cs`, `src/Controls/src/Core/UriImageSource.cs`, `src/Core/src/Hosting/ImageSources/ImageSourceToImageSourceServiceTypeMapping.cs`, `src/Core/AndroidNative/maui/src/main/java/com/microsoft/maui/PlatformInterop.java`, `…/glide/MauiGlideModule.java`, `…/glide/MauiCustomViewTarget.java` — https://github.com/dotnet/maui
- NuGet, `Microsoft.Maui.Core` 10.0.50 (beroende `Xamarin.Android.Glide` 4.16.0.14): https://www.nuget.org/packages/Microsoft.Maui.Core/10.0.50
- Glide 4.16.0: `Glide.java` (`init(Context, GlideBuilder)`, `@VisibleForTesting`), `DiskCache.java`, `RequestBuilder.java` (`preload`, `submit`), `CustomViewTarget.java` — https://github.com/bumptech/glide/tree/v4.16.0/library/src/main/java/com/bumptech/glide
- Apple, `containerURL(forSecurityApplicationGroupIdentifier:)`: https://developer.apple.com/documentation/foundation/filemanager/containerurl(forsecurityapplicationgroupidentifier:)
- Microsoft, `BitmapImage.DecodePixelWidth`: https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.media.imaging.bitmapimage.decodepixelwidth (UWP-sidan. WinUI-motsvarigheten i `Microsoft.UI.Xaml.Media.Imaging` är inte öppnad härifrån.)
- BlurHash (Wolt): https://github.com/woltapp/blurhash — .NET: https://github.com/MarkusPalcer/blurhash.net, https://www.nuget.org/packages/Blurhash.Core, https://www.nuget.org/packages/Blurhash.SkiaSharp
- ThumbHash (Evan Wallace): https://github.com/evanw/thumbhash — .NET: https://github.com/jzebedee/ThumbHash, https://www.nuget.org/packages/ThumbHash
