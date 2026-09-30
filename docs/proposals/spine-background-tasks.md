# Bakgrundsuppgifter i Spine (förstudie, rev 1)

**Status:** Förstudie, med ägarens beslut från 2026-09-30 i avsnittet [Beslut](#beslut-2026-09-30). Inget implementerat. Issue: [#308](https://github.com/jonatansoderberg/Maui.Spine/issues/308), prioriterad i [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317) som P2 med frågorna *registreringstidpunkten på iOS* och *Windows*.
**Fråga:** Kan Spine erbjuda attributupptäckta bakgrundsuppgifter (`[BackgroundTask]` + `IBackgroundTask`) över BGTaskScheduler och Androids schemaläggare, så att widgetar och Live Activities hålls färska utan att appen är i förgrunden — och hur löser man att iOS kräver registrering innan appen har startat klart?
**Svar:** Ja, som ett eget paket `Plugin.Maui.Spine.BackgroundTasks`, med attributet upptäckt vid körning precis som `[Widget]`. Men **inte med en iOS-identifierare per uppgift**: iOS tillåter en enda väntande refresh-begäran per app och kräver att varje identifierare i `Info.plist` har en handler registrerad innan `didFinishLaunching` returnerar. Spine registrerar därför två fasta identifierare (`<ApplicationId>.spine.refresh` och `.spine.processing`) i MAUI:s `FinishedLaunching`-händelse och väljer själv, i C#, vilka uppgifter som står på tur. Då behövs ingen byggtidsupptäckt, registreringstidpunkten blir Spines ansvar i stället för appens, och Widgets befintliga bakgrundskörning flyttar in som en uppgift bland andra. Android: `JobScheduler` direkt, inte WorkManager. Windows och Mac Catalyst får inget OS-schema i v1. Där körs uppgifterna bara medan appen kör, och det ska stå i klartext.

---

## 1. Slutsatsen i korthet

| Fråga | Svar | Belägg |
|---|---|---|
| Hinner Spine registrera i tid på iOS? | **Ja.** MAUI bygger `MauiApp` i `willFinishLaunching` och anropar lifecycle-händelsen `FinishedLaunching` *inuti* `didFinishLaunching`, före `return true`. Widgets registrerar redan sin `BGAppRefreshTask` där. | `MauiUIApplicationDelegate.cs` (dotnet/maui), `SpineWidgetsExtensions.Apple.cs:31-33` |
| En identifierare per `[BackgroundTask]`? | **Nej.** Apple: *"There can be a total of 1 refresh task and 10 processing tasks scheduled at any time."* Två paket med var sin refresh-identifierare tränger undan varandra. Spine multiplexar uppgifterna över en refresh- och en processing-identifierare. | `submit(_:)`-dokumentationen, §4 |
| Behövs byggtidsupptäckt (source generator, `<SpineBackgroundTask>`-items)? | **Nej**, just därför att identifierarna är fasta. Byggsteget skriver två strängar och en bakgrundsmod; allt annat är reflektion vid start, som för sidor och widgetar. | §5.3 |
| Eget paket eller i kärnan? | **Eget paket**, kontrakten i `Plugin.Maui.Spine.Common` så att Push och Widgets kan anropa dem utan hårt beroende (samma mönster som `IWidgetService`). Widgets tar ett beroende på paketet och lämnar ifrån sig sin BGTask-kod. | §5.1 |
| WorkManager på Android? | **Nej, `JobScheduler`.** Det finns i Mono.Android, ger periodiskt ≥ 15 min, nätverks- och laddvillkor, överlever omstart och har expedierade jobb från API 31. WorkManager-bindningen drar med sig Room, Kotlin-korutiner och Lifecycle 2.11 — exakt de paket Push redan fått tvinga ihop versionerna på — och #171 valde bort WorkManager av samma skäl. **Ägarens beslut**, se §11. | nuspec för `Xamarin.AndroidX.Work.Runtime` 2.11.2.1, `Directory.Packages.props`, `issues/171-…md` |
| Windows? | **En timer i processen** medan appen kör, plus ikappkörning vid start. `BackgroundTaskBuilder` i Windows App SDK kräver MSIX, och Spines appar kör `WindowsPackageType=None`. | Microsofts dokumentation (§3.4), `samples/*/…csproj` |
| Mac Catalyst? | **Samma som Windows.** API:et finns, men Apple DTS (nov 2025): *"the BackgroundTask framework will not launch your app on macOS"*. | Apple Developer Forums 807388 |
| Håller det widgetar färska? | **Bättre än i dag, aldrig exakt.** iOS väljer själv när en refresh körs; realtid kräver push (#287-vägen). Det uppgiften löser är att data hämtas och trädet byggs om *utan* att appen öppnas. | §8, `docs/wiki/widgets.md` |

---

## 2. Vad som redan finns i Spine

Spine har redan en bakgrundskörning, men bara för widgetar och bara en:

| Del | Var | Vad det betyder här |
|---|---|---|
| `IBackgroundRefreshHandler` + `UseBackgroundRefresh<T>()` | `Common/Core/IBackgroundRefreshHandler.cs:15`, `SpineWidgetsOptions.cs:31,48` | Förlagan till `IBackgroundTask`: en metod, en token, upplöst via DI vid varje körning. Blir en adapter (§8). |
| Registrering på iOS | `SpineWidgetsExtensions.Apple.cs:21-25` läser identifieraren ur `Info.plist`, `:31-33` registrerar i `FinishedLaunching`, `:137-160` sätter `ExpirationHandler` och bokar nästa körning *innan* arbetet börjar | Rätt ordning och rätt tidpunkt, och det är just den koden som flyttar. |
| Bokning på iOS | `:45-49` (`DidEnterBackground`), `:162-167` (`BGAppRefreshTaskRequest` med `EarliestBeginDate`) | Samma mönster, men med tidigaste förfallotid över alla uppgifter. |
| Byggsteget, iOS | `spine-widgets-build.sh:264-265` skriver `UIBackgroundModes: fetch` och `<ApplicationId>.spine-widgets.refresh` i `HostManifest.plist`, som blir `PartialAppManifest` (`Plugin.Maui.Spine.Widgets.targets:129`) | Upptar appens enda refresh-plats. Två refresh-identifierare kan inte samexistera (§4). |
| Android | `SpineBackgroundReceiver.cs:50-64`: inexakt alarm (`SetAndAllowWhileIdle`), bokat vid `OnStop` (`SpineWidgetsExtensions.Android.cs:39`) och efter varje körning | Fungerar, men se de två fynden nedan. |
| Attributupptäckt | `WidgetRegistry.cs:13-32` och `RegisterNavigables` (`MauiAppBuilderExtensions.cs:195`) skannar `SpineOptions.Assemblies` med reflektion | `BackgroundTaskRegistry` blir en tredje likadan skanning. |
| Modulregistrering | `build/<Paket>.props` med `<SpineModule>` → `SpineModules.g.cs` med `[ModuleInitializer]` → `UseSpine()` kör modulerna sist (`MauiAppBuilderExtensions.cs:157`, issue #355) | Paketet registrerar sig självt, och därmed sina lifecycle-hookar, i tid. |
| Tidiga hookar i Push | `FinishedLaunching` (`SpinePushNotificationsExtensions.Apple.cs:22`), `OnApplicationCreate` (`…Android.cs:17`), `SpinePushNotifications.Install()` före `UIApplication.Main` (`SpinePushNotifications.Apple.cs:53`) | Visar hur tidigt Spine redan når: Install behövs för delegatmetoder UIKit läser vid tilldelning. BGTaskScheduler behöver inte gå så tidigt. |
| Push som väcker arbete | `HandleInternallyAsync` bygger om widgetar för `PushKind.Widget` (`SpinePushNotificationsExtensions.cs:164-178`); FCM ger 20 s (`SpinePushNotificationsMessagingService.cs:43`) | Naturlig ingång för `spine.task` (§8). |
| Bakgrundsstart genom widgetknapp | #218: iOS startar appens process i bakgrunden för `SpineWidgetIntent`. Kallstarten tog ~4 s i simulatorn | Samma kallstart betalas av varje BGTask som startar en död app. Och en bakgrundsstart är precis det läge där sen registrering kraschar (§4). |
| Delat plist-/entitlements-steg | `Plugin.Maui.Spine.Common.targets:1-24`: `<SpineEntitlement>`-items, ett mål skriver filen | Mönstret som behövs för `UIBackgroundModes` också. |

**Två fynd i befintlig kod** som en implementation måste ta hand om. Båda är belägg ur källkoden, inte verifierade i ett bygge eller på enhet:

1. **`UIBackgroundModes` skrivs av två `PartialAppManifest` och den ena vinner.** Push skriver `remote-notification` (`Plugin.Maui.Spine.PushNotifications.targets:76-77`), Widgets skriver `fetch`. SDK:ns `MergePartialPlistDictionary` (dotnet/macios, `CompileAppManifest.cs`) slår bara ihop *dictionaries*. En array ersätts. En app med båda paketen, som push-samplet, får alltså bara den ena moden. Den som förlorar `remote-notification` får inga tysta pushar i bakgrunden, och den som förlorar `fetch` får aldrig sin `BGAppRefreshTask`. Kontroll: `plutil -p` på det byggda push-samplets `Info.plist`. Åtgärd: `<SpineBackgroundMode>`-items i `Common.targets`, ett mål skriver en plist, som `<SpineEntitlement>`.
2. **Android-körningen saknar tidsgräns och överlever inte omstart.** `SpineBackgroundReceiver` kör handlern med `CancellationToken.None` bakom `goAsync()` (`:36-42`), men Android förväntar sig att en broadcast avslutas *"very quickly (under 10 seconds)"*. Alarmet bokas bara vid `OnStop`, och alarm nollställs vid omstart (som redan konstaterats i `spine-local-notifications.md` §6). Efter en omstart står bakgrundskörningen alltså still tills appen öppnas.

---

## 3. Plattformarnas byggstenar

### 3.1 iOS

- **`BGAppRefreshTask`**: kort, *"small bits of information"*. Kräver `UIBackgroundModes: fetch`. Budgeten är omkring 30 s. Det står i Spines egen dokumentation (`IBackgroundRefreshHandler.cs`) och är vedertaget, men står inte i Apples API-referens.
- **`BGProcessingTask`**: minuter, men *"Processing tasks run only when the device is idle. The system terminates any background processing tasks running when the user starts using the device."* Kräver `UIBackgroundModes: processing`. Begäran kan kräva nätverk (`RequiresNetworkConnectivity`) och laddning (`RequiresExternalPower`).
- **`BGContinuedProcessingTask`** (iOS 26): startas från förgrunden *"in response to someone's action"* och fortsätter i bakgrunden med förlopp i en systemritad Live Activity. Det är inte schemaläggning, men det passar "exportera/ladda upp nu". Bundet i Microsoft.iOS, men som `NoMacCatalyst`, trots att Apples referens anger Mac Catalyst 26.
- **Info.plist:** `BGTaskSchedulerPermittedIdentifiers`. *"Every identifier in the [list] requires a handler."*
- **Kvoter:** en väntande refresh-begäran och tio processing-begäranden per app. `EarliestBeginDate` är ett golv, aldrig en tid: *"the system doesn't guarantee launching the task at the specified date, but only that it won't begin sooner."*
- **Bindningen:** `BackgroundTasks` finns i Microsoft.iOS, och Widgets använder den redan. I macios `main` är `Submit(request, out error)` markerad som föråldrad från iOS 27, till förmån för en variant med completion handler. Spine anropar den gamla (`SpineWidgetsExtensions.Apple.cs:165`).

### 3.2 Mac Catalyst

Klasserna är tillgängliga från Catalyst 13.1, men systemet startar inte appen för dem. Apples DTS svarade i november 2025: *"No. More specifically, the BackgroundTask framework will not launch your app on macOS, though I believe your tasks may run if your app is running already."* Rekommendationen där är en LaunchAgent via `SMAppService`, vilket ligger utanför Spine. Därför gäller samma modell som på Windows (§3.4).

### 3.3 Android

- **`JobScheduler`** (Mono.Android): `setPeriodic` med 15 minuter som golv, nätverks-, ladd- och idle-villkor, `setPersisted` (kräver `RECEIVE_BOOT_COMPLETED`, som Push redan deklarerar i `Permissions.cs:7`) och `setExpedited` från API 31. Jobbet körs i appens process i en `JobService` (`BIND_JOB_SERVICE`). Jobbens exakta tidsgräns har inte kontrollerats för den här studien.
- **WorkManager** (`Xamarin.AndroidX.Work.Runtime` 2.11.2.1 för `net10.0-android36.0`): samma 15-minutersgolv. Ovanpå det finns unika köer, backoff, expedierat arbete med fallback (`OutOfQuotaPolicy`, och `getForegroundInfo` krävs på Android 11 och äldre), 10 minuter per worker och *"long-running"* via `setForeground`. Från Android 16 kan långkörande workers dock *"exhaust your app's job quota"*. Den persisterar i en egen SQLite-databas och bokar om efter omstart.
- **Processen:** `MauiApplication.OnCreate` bygger `MauiApp` och skapar `IApplication` (appens `App`) innan en tjänst eller mottagare körs (dotnet/maui `MauiApplication.cs`). DI finns alltså när ett jobb körs, men appens `App`-konstruktor körs också i varje bakgrundsstart.

### 3.4 Windows

`Microsoft.Windows.ApplicationModel.Background.BackgroundTaskBuilder` i Windows App SDK registrerar en COM-klass som `backgroundtaskhost.exe` aktiverar, med triggers och villkor (`InternetAvailable` med flera). Microsofts egen dokumentation säger: *"Background tasks using the Windows App SDK `BackgroundTaskBuilder` require your app to be packaged with MSIX."* Utan MSIX hänvisar Microsoft till Task Scheduler. Det skulle starta hela MAUI-appen med fönster, och det kräver en huvudlös startväg som Spine inte har. Spines sampel kör `WindowsPackageType=None`. Vilken Windows App SDK-version som införde klassen har inte kontrollerats.

---

## 4. Registreringstidpunkten på iOS

**Kravet.** *"Registration of all launch handlers must be complete before the end of `applicationDidFinishLaunching(_:)`."* Ett brott mot kravet ger `NSInternalInconsistencyException: All launch handlers must be registered before application finishes launching`, och det är en krasch, inte en varning. Dubbelregistrering är lika illa: *"The system kills the app on the second registration of the same task identifier."*

**Varför det spelar roll just i Spine.** Kraschen syns först när appen startas *i bakgrunden*. I Apple-forumtråden 775182 (iOS 18.4) kom den från SwiftUI:s `.backgroundTask`, som registrerade för sent när appen startades av en widget, en genväg eller Kontrollcenter. Apples råd var att registrera i `didFinishLaunchingWithOptions`. Spines widgetknappar startar appen i bakgrunden (#218), så det här är ett scenario som faktiskt inträffar i Spine-appar.

**Var MAUI placerar oss.** `MauiUIApplicationDelegate.WillFinishLaunching` anropar `CreateMauiApp()`, och därmed `UseSpine()` och modulerna. `FinishedLaunching` sätter applikationshanteraren, skapar fönstret *om appen saknar scenmanifest*, anropar `iOSLifecycle.FinishedLaunching`-händelserna och returnerar först därefter `true`. En registrering i `ios.FinishedLaunching(...)` som ett paket lagt till via `ConfigureLifecycleEvents` sker alltså i tid. Widgets gör redan så. Det här är läst ur MAUI:s källkod på `main`, inte stegat i en debugger.

**Det som *inte* är i tid**, och därför inte får förekomma:

- Registrering när `IBackgroundTasks` löses upp första gången. DI-upplösningen kan ske långt efter start.
- Registrering från `App.CreateWindow`, `OnStart` eller en sidas `OnAppearing`. Med scenmanifest skapas fönstret i scenens `WillConnect`, efter `didFinishLaunching`.
- Registrering som beror på att appen själv har anropat en `UseXxx()`. Modulen gör det via `UseSpine`, och en app utan `UseSpine` måste anropa `UseSpineBackgroundTasks()` i `MauiProgram`, vilket fortfarande sker i `willFinishLaunching`.

**Varför fasta identifierare löser resten.** Identifierarna måste stå i `Info.plist` vid bygget, men `[BackgroundTask]` upptäcks vid körning. Med en identifierare per uppgift skulle byggsteget behöva känna till uppgifterna. Då krävs antingen dubbeldeklarationen som widgetar har (`<SpineWidget>` i csproj *och* `[Widget]` i kod, `WidgetAttribute.cs:4-6`) eller en source generator. Med två fasta identifierare räcker det att byggsteget vet att paketet är refererat. `FinishedLaunching` registrerar alltid exakt de två som står i plist-filen, så *"every identifier requires a handler"* gäller per konstruktion. Kvoten på en refresh-begäran tvingar fram multiplexen ändå.

**Konsekvens för Widgets.** `…spine-widgets.refresh` och Spines nya refresh-identifierare kan inte samexistera. Båda skulle upptäcka att den andra ockuperar platsen, och `Submit` skulle misslyckas med *too many pending*. Därför måste bakgrundspaketet äga *den enda* registreringen, och Widgets blir en klient (§8). Under övergången får båda paketen aldrig registrera samma identifierare, eftersom det dödar appen.

---

## 5. Alternativ och avvägningar

### 5.1 Placering

| | Eget paket (förslag) | I kärnan `Plugin.Maui.Spine` | Kvar i Widgets |
|---|---|---|---|
| Vem betalar | Bara appar som refererar paketet får `fetch`-moden och ett nytt manifest-element | Varje Spine-app, eller en opt-in-flagga i kärnans targets | Bara widget-appar |
| Push/Widgets når det | Via `Common`-kontrakt och `GetService`, som `IWidgetService` i dag | Direkt | Push når det via `IWidgetService`, men en app utan widgetar kan inte använda det |
| Orientera "prefetch över natten" (ingen widget) | Ja | Ja | Nej |
| Risk | Widgets får ett beroende till | Kärnan får plist- och manifestplumbing den inte har i dag | Fel hem |

Eget paket, med `Widgets → BackgroundTasks → Common`. Kärnan är ett rimligt alternativ om ägaren hellre vill slippa ett paket till: på Android blir tillägget litet, eftersom `JobScheduler` inte drar in några beroenden.

### 5.2 Attribut eller builder-API

Attributet följer `[Widget]`, `[NavigableRegion]` och `[NavigableTab]`: identitet och standardpolicy står på klassen, och upptäckten sker via `SpineOptions.Assemblies`. Ett rent builder-API (`o.AddTask<T>("name", every: …)`) skulle vara det enda i Spine som registrerar en typ för hand. **Attribut för identitet och standard, options för det som ändras vid körning**, till exempel intervallet ur en användarinställning.

Issuens `Interval = "00:15"` fungerar inte som det står. Ett attribut kan inte ta en `TimeSpan`, och `"00:15"` kan läsas både som 15 minuter och 15 sekunder. Förslaget är `IntervalMinutes = 15`.

### 5.3 En identifierare per uppgift eller multiplex

Med en identifierare per uppgift skulle iOS få bestämma ordningen, men kvoten på en refresh-begäran gör det omöjligt för mer än en kort uppgift, och varje uppgift skulle kräva byggtidskunskap. Med multiplex bestämmer Spine: vid varje refresh körs de uppgifter som är förfallna, mest försenad först, tills tiden tar slut. Priset är att en långsam uppgift kan äta upp budgeten för de andra. Uppgifter som är markerade `Long` går därför till processing-identifieraren i stället.

### 5.4 Android: alarm, JobScheduler eller WorkManager

| | Alarm (i dag) | JobScheduler (förslag) | WorkManager |
|---|---|---|---|
| Villkor (nätverk, laddning) | Nej | Ja | Ja |
| Överlever omstart | Nej, kräver en egen boot-mottagare | `setPersisted` | Ja |
| Tid per körning | Under ~10 s (`goAsync`) | Jobbets gräns (ej kontrollerad) | 10 min, längre via `setForeground` |
| På begäran, snabbt | Nej | `setExpedited` (API 31+) | `setExpedited` med fallback |
| Beroenden | Inga | Inga | Work.Runtime → Room, Kotlin-korutiner, Lifecycle 2.11, Startup |

JobScheduler ger det som saknas utan att öppna versionsbråket i `Directory.Packages.props` (Push-blocket) en gång till. WorkManager är det bättre valet om Spine vill ha retries/backoff och Android 16-kvothantering "gratis". Därför är valet ett ägarbeslut (§11).

---

## 6. Föreslagen API-yta

Kontrakten ligger i `Plugin.Maui.Spine.Common` (net10.0, utan MAUI), där `IWidgetService` också ligger:

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BackgroundTaskAttribute(string name) : Attribute
{
    /// <summary>Stable name; also what <see cref="IBackgroundTasks.RequestAsync"/> and a push's <c>spine.task</c> use.</summary>
    public string Name { get; } = name;

    /// <summary>Minutes between scheduled runs. 0 runs it only when requested. Android's floor is 15.</summary>
    public int IntervalMinutes { get; set; }

    public bool RequiresNetwork { get; set; }
    public bool RequiresCharging { get; set; }

    /// <summary>Minutes of work: a BGProcessingTask on iOS (only while the device is idle), a normal job on Android.</summary>
    public bool Long { get; set; }

    /// <summary>Widget kinds rebuilt after a run that completed.</summary>
    public string[] Widgets { get; set; } = [];
}

public interface IBackgroundTask
{
    Task RunAsync(BackgroundTaskRun run, CancellationToken cancellationToken);
}

public sealed record BackgroundTaskRun(string Name, BackgroundTaskTrigger Trigger, DateTimeOffset? LastCompleted);

public enum BackgroundTaskTrigger { Scheduled, Requested, Push, CatchUp }

public interface IBackgroundTasks
{
    /// <summary>Whether the platform runs tasks while the app is closed. False on Windows and Mac Catalyst.</summary>
    bool RunsWhileClosed { get; }

    IReadOnlyList<string> Names { get; }

    /// <summary>Runs <paramref name="name"/> now when the app is running; otherwise asks the platform to run it soon.</summary>
    Task RequestAsync(string name, CancellationToken cancellationToken = default);

    Task RequestAsync<TTask>(CancellationToken cancellationToken = default) where TTask : IBackgroundTask;

    /// <summary>When it last ran and how it went: the answer to "why is my widget old?".</summary>
    BackgroundTaskStatus StatusOf(string name);
}
```

I appen:

```csharp
[BackgroundTask("standings", IntervalMinutes = 30, RequiresNetwork = true, Widgets = ["team"])]
public sealed class StandingsTask(IStandingsApi _api, StandingsStore _store) : IBackgroundTask
{
    public async Task RunAsync(BackgroundTaskRun run, CancellationToken cancellationToken) =>
        await _store.SaveAsync(await _api.FetchAsync(cancellationToken), cancellationToken);
}

builder.UseSpineBackgroundTasks(o => o.Interval("standings", settings.RefreshEvery)); // valfritt, TimeSpan.Zero stänger av
```

**Byggsteget.** `SpineBackgroundTasksEnabled` är `true` som standard och ger en `fetch`-mod och `<ApplicationId>.spine.refresh`. `SpineBackgroundTasksProcessing` är `false` som standard och lägger till `processing` och `.spine.processing`. Processing är opt-in eftersom en bakgrundsmod appen inte använder inte ska deklareras. Vid start loggar Spine en varning för en `Long`-uppgift utan processing-identifierare och kör den som refresh, på samma sätt som Widgets validerar `[Widget]` mot `<SpineWidget>`. På Android skrivs `JobService` och `RECEIVE_BOOT_COMPLETED` i manifest-overlayen.

**Schemat** persisteras per uppgift (senast startad, senast klar, resultat, tidigast nästa) i `Preferences`, via en `JsonSerializerContext` som i lokala notiser. Det är vad `StatusOf` läser, och vad multiplexen använder på iOS för att välja uppgifter.

---

## 7. DI-scope och vad en uppgift får röra

- **Ett scope per körning.** `await using var scope = services.CreateAsyncScope()`, därefter `ActivatorUtilities.CreateInstance(scope.ServiceProvider, type)`, och scopet släpps efter körningen. Widgets skapar i dag handlers från rot-providern (`SpineWidgetsExtensions.cs:47,61,89`), vilket gör att transienta `IDisposable` lever tills processen dör.
- **Trådpoolen, inte huvudtråden.** Widgetknappar körs på huvudtråden (`:87`) eftersom de kan röra appens tillstånd. En bakgrundsuppgift ska inte göra det, och huvudtråden kan vara upptagen med att bygga ett fönster som ingen ser.
- **Tillåtet:** `HttpClient`, filer, `Preferences`, databaser, `IWidgetService`, `ILiveActivityService.UpdateAsync` och `ILocalNotificationService.SyncAsync`. Det sista är det Orientera behöver: planera om notiserna efter en synk.
- **Inte tillåtet:** `INavigationService`, sidor, dialoger och tillståndsfrågor (`RequestPermissionAsync` visar en systemdialog). Det gäller också allt som förutsätter ett synligt fönster. Spine kan inte hindra det i typsystemet. Därför loggar dispatchern en varning om en uppgift löser upp `INavigationService` i sitt scope, och wikin säger det i första stycket.
- **Samtidighet:** högst en körning per namn, med ett lås per uppgift. En `RequestAsync` i förgrunden och en BGTask-start får inte dubbelköra.
- **Tidsgräns:** den token som skickas in avbryts av `ExpirationHandler` på iOS och av `onStopJob` på Android. En uppgift som struntar i token blir dödad, och Spine anropar då `SetTaskCompleted(false)` eller `jobFinished(…, true)`, precis en gång.
- **Kallstart:** en bakgrundsstart kör `CreateMauiApp`, appens `App`-konstruktor och, på iOS utan scenmanifest, även `CreatePlatformWindow`. #218 mätte ~4 s i simulatorn. Det ryms i budgeten men ska stå i wikin: tung start i `App` stjäl tid från uppgiften.

---

## 8. Koppling till widgets och push

Det är den här kopplingen som gör paketet värt att bygga (#317: *"background tasks are what keep widget timelines fresh"*). WidgetKit-extensionen kör ingen .NET, och en `Refresh(after)` på iOS läser bara om JSON-filen eller hämtar en `RemoteSource`. Ny data blir bara till när appens process kör. Uppgiften är den processen.

- **`Widgets = ["team"]`** på attributet: efter en körning som slutförts anropar dispatchern `IWidgetService.RefreshAsync(kind)` via `GetService`. På iOS skriver det trädet och anropar `reloadTimelines` genom bryggan, och på Android `AppWidgetManager` via `RemoteViewsRenderer`. En uppgift kan också anropa tjänsten själv.
- **Widgets egen bakgrundskörning blir en inbyggd uppgift**, `spine.widgets`: först `IBackgroundRefreshHandler` om en sådan finns, sedan `RefreshAllAsync`, med intervallet `BackgroundRefreshInterval`. Det publika API:et är oförändrat. `SpineWidgetsBackgroundRefresh` och `.spine-widgets.refresh` pensioneras, och Widgets slutar registrera något själv.
- **Push:** en ny nyckel `spine.task=<namn>` på `Silent`- och `Widget`-meddelanden. `HandleInternallyAsync` anropar `IBackgroundTasks.RequestAsync` med `Trigger = Push`. På iOS körs uppgiften direkt inom den tysta pushens fönster (~30 s). Den kan inte köras längre än så, och det finns ingen iOS-väg att lämna över långt arbete från en push. På Android kan den läggas som ett expedierat jobb som lever vidare efter FCM:s 20 s. Issuens användningsfall *"offload long work from the notification handler"* går alltså bara att uppfylla på Android.
- **Förväntningar per app.** Puckkoll var 15:e minut är en önskan, inte ett löfte. Realtiden kommer från push, som #287 redan visade under en match. Almanackas bilder "vid midnatt" görs säkrast i förväg, som framtida timeline-poster och roterande assets. En uppgift som ska köras exakt vid midnatt gör inte det på iOS.

---

## 9. Plattformar

| | Schemalagt | På begäran | Från push | Byggsten | Villkor |
|---|---|---|---|---|---|
| iOS | Ja, när systemet vill; multiplexat | I förgrunden direkt, annars som `EarliestBeginDate = nu` utan garanti | Inom pushens ~30 s | `BGAppRefreshTask`, `BGProcessingTask` | Nätverk och laddning bara för `Long` |
| Mac Catalyst | Bara medan appen kör | Direkt om appen kör | Som iOS om appen kör | Timer i processen | Nej |
| Android | Ja, ≥ 15 min | Expedierat (API 31+) | Expedierat jobb | `JobScheduler` | Nätverk, laddning |
| Windows | Bara medan appen kör | Direkt | Push finns inte på Windows i Spine | `PeriodicTimer` i processen | Nej |

Alla plattformar får en **ikappkörning vid start och vid återkomst till förgrunden**: förfallna uppgifter körs med `Trigger = CatchUp`. På Windows och Catalyst är det huvudvägen, på iOS och Android ett skyddsnät.

---

## 10. Vad som inte går, och vad som inte är verifierat

Studien gjordes i en Linux-container, utan Mac, utan enhet och utan Windows. **Inget är provat.** Allt nedan kommer från dokumentation och källkod.

1. **iOS-tidpunkter är systemets.** Det finns ingen körning var 15:e minut, och ingen alls om användaren tvångsavslutat appen eller stängt av Bakgrundsuppdatering. Det står i wikin i första stycket, inte i en fotnot.
2. **Multiplexens budget.** Att alla förfallna refresh-uppgifter ryms på ~30 s är ett antagande. Hur ofta iOS faktiskt ger en refresh till en app vars *enda* identifierare bär flera uppgifter är inte mätt.
3. **Registreringstidpunkten** i §4 är läst ur MAUI:s källkod på `main`, inte stegad. Den ska verifieras med en bakgrundsstart via widgetknapp *och* via `_simulateLaunchForTaskWithIdentifier:` på en fysisk enhet. BGTask-starter kan inte provas i simulatorn (`docs/wiki/widgets.md`).
4. **`UIBackgroundModes`-sammanslagningen** (§2, fynd 1) är en slutsats ur SDK-koden. Den ska bekräftas med `plutil -p` på push-samplets bygge innan något annat görs.
5. **Mac Catalyst** vilar på ett DTS-svar i ett forum, inte på dokumentation. Om Apple ändrar beteendet ändras bara `RunsWhileClosed`.
6. **JobScheduler-detaljer**: tidsgränsen per jobb, vilka villkor som tillåts med `setExpedited` och kvotbeteendet i Android 16 är inte kontrollerade. WorkManager-siffrorna (10 min, 15 min) är det.
7. **Android-kallstart:** kan ett jobb startas innan `MauiApplication.OnCreate` är klar? `JobService.onStartJob` körs på huvudtråden efter `Application.onCreate` enligt Androids modell, men det är inte provat med MAUI. Dispatchern bör vänta på `IPlatformApplication.Current` i stället för att anta att den finns.
8. **Skyddade filer på iOS**: en uppgift som körs medan enheten är låst kan inte läsa filer med `NSFileProtectionComplete`. Vad MAUI:s `Preferences` och `SecureStorage` använder som standard är inte kontrollerat.
9. **`BGContinuedProcessingTask`** är med som möjlig v2. Bindningen och Apples dokumentation är oense om Catalyst.

---

## 11. Leveransplan

**Beslut som behövs från ägaren innan steg 2:**
- Eget paket eller kärnan (§5.1). Förslag: eget paket.
- JobScheduler eller WorkManager (§5.4). Förslag: JobScheduler, i linje med beslutet i #171.
- Får Widgets bryta med `SpineWidgetsBackgroundRefresh` och `.spine-widgets.refresh` i en minor-version, eller ska de ligga kvar ett varv som alias?
- Ska `RequestAsync` i förgrunden köra direkt (förslag) eller alltid gå via plattformen?

**Steg:**
1. **Rätta `UIBackgroundModes`** oavsett resten: `<SpineBackgroundMode>` i `Common.targets`, där Push och Widgets bidrar med items. Verifiera med `plutil -p` på push-samplet.
2. **Kontrakt och dispatcher**: `Common`-typerna, `BackgroundTaskRegistry`, det persisterade schemat, ett scope per körning, lås, `StatusOf`, ikappkörning och timern för Windows och Catalyst. Allt utom plattformsdelen kan enhetstestas på net10.0.
3. **iOS**: två fasta identifierare som registreras i `FinishedLaunching`, multiplex, `ExpirationHandler`, bokning vid `DidEnterBackground` och efter varje körning, och plist-nycklarna via steg 1.
4. **Android**: `JobService`, ett jobb per uppgift (stabilt id, som `AndroidNotifications.StableId`), villkor, `setPersisted` och expedierat jobb på begäran.
5. **Widgets flyttar in**: `spine.widgets` som inbyggd uppgift, `IBackgroundRefreshHandler` som adapter, `SpineBackgroundReceiver` och BGTask-koden bort, samt `Widgets = [...]` på attributet.
6. **Push**: `spine.task` i `HandleInternallyAsync`, och på sikt `IPushSender.RunTaskAsync` i `Plugin.Maui.Spine.Server`.
7. **Sampel, wiki, skill och enhet**: en uppgift i push-samplet som skriver en stämpel i widgeten. På iPhone via LLDB `_simulateLaunchForTaskWithIdentifier:` och `_simulateExpirationForTaskWithIdentifier:`, på Android via `adb shell cmd jobscheduler run -f <paket> <id>`. Därefter ett dygn i Puckkoll med `StatusOf` loggat, för att få verkliga siffror på hur ofta iOS faktiskt kör.

---

## Beslut (2026-09-30)

Jonatan gick igenom studiens frågor 2026-09-30 och följde rekommendationerna. Rader märkta **Förslag** saknade rekommendation i studien; där står ett förslag med skäl, som gäller tills han säger annat.

- **Placering.** Eget paket `Plugin.Maui.Spine.BackgroundTasks`, med kontrakten i Common (§5.1).
- **Android.** `JobScheduler`, inte WorkManager, i linje med #171 (§5.4).
- **Widgets gamla ytor** — **Förslag.** `SpineWidgetsBackgroundRefresh` och `.spine-widgets.refresh` ligger kvar ett varv som alias, med en byggvarning som pekar på det nya, och tas bort i versionen efter. Paketen ligger på nuget.org och Puckkoll, Almanacka och Orientera följer dem via `SpineVersion`; en identifierare som försvinner i en minor slutar köra tyst i bakgrunden i stället för att ge ett byggfel.
- **`RequestAsync` i förgrunden.** Kör uppgiften direkt, utan omväg via plattformen.
- **iOS-identifierare.** Två fasta (`.spine.refresh` och `.spine.processing`) som Spine multiplexar, inte en per uppgift (§5.3).
- **Deklaration.** Attribut för identitet och standardvärden, options för ändringar i körtid. `IntervalMinutes = 15` i stället för issuens `Interval = "00:15"` (§5.2).
- **Windows och Mac Catalyst.** Timer i processen och ikappkörning vid start, synligt som `RunsWhileClosed = false`.
- **`processing`-läget.** Opt-in: `SpineBackgroundTasksProcessing=false` som standard.
- **`UIBackgroundModes`.** Rättas först, oavsett resten (steg 1). Felet är bekräftat på ett bygge av push-samplet 2026-09-30: [#435](https://github.com/jonatansoderberg/Maui.Spine/issues/435).
- **Push.** Nyckeln `spine.task=<namn>` triggar en uppgift; `IPushSender.RunTaskAsync` kommer senare.
- **`BGContinuedProcessingTask`.** Inte i v1.

---

## 12. Referenser

Lästa i repot: `SpineWidgetsExtensions.Apple.cs`, `SpineWidgetsExtensions.cs`, `SpineBackgroundReceiver.cs`, `SpineWidgetsExtensions.Android.cs`, `WidgetRegistry.cs`, `Plugin.Maui.Spine.Widgets.targets`, `spine-widgets-build.sh`, `Plugin.Maui.Spine.PushNotifications.targets`, `Plugin.Maui.Spine.Common.targets`, `Plugin.Maui.Spine.targets`, push-paketets lifecycle-filer, `MauiAppBuilderExtensions.cs`, `docs/wiki/widgets.md`, `docs/wiki/push-notifications.md`, `issues/171-…`, `issues/218-…`, `issues/287-…`, `issues/355-…`.

- Apple, `register(forTaskWithIdentifier:using:launchHandler:)`: https://developer.apple.com/documentation/backgroundtasks/bgtaskscheduler/register(fortaskwithidentifier:using:launchhandler:)
- Apple, `submit(_:)` (1 refresh + 10 processing): https://developer.apple.com/documentation/backgroundtasks/bgtaskscheduler/submit(_:)
- Apple, `BGAppRefreshTask` / `BGProcessingTask`: https://developer.apple.com/documentation/backgroundtasks/bgapprefreshtask, https://developer.apple.com/documentation/backgroundtasks/bgprocessingtask
- Apple, *Using background tasks to update your app*: https://developer.apple.com/documentation/uikit/using-background-tasks-to-update-your-app
- Apple, `earliestBeginDate`: https://developer.apple.com/documentation/backgroundtasks/bgtaskrequest/earliestbegindate
- Apple, *Performing long-running tasks on iOS and iPadOS* (`BGContinuedProcessingTask`): https://developer.apple.com/documentation/backgroundtasks/performing-long-running-tasks-on-ios-and-ipados
- Apple Developer Forums, Mac Catalyst och BackgroundTasks (DTS, nov 2025): https://developer.apple.com/forums/thread/807388
- Apple Developer Forums, kraschen vid sen registrering (iOS 18.4): https://developer.apple.com/forums/thread/775182
- dotnet/macios, BackgroundTasks-bindningen: https://github.com/dotnet/macios/blob/main/src/backgroundtasks.cs
- dotnet/macios, `CompileAppManifest.cs` (sammanslagning av partiella plist-filer): https://github.com/dotnet/macios/blob/main/msbuild/Xamarin.MacDev.Tasks/Tasks/CompileAppManifest.cs
- dotnet/maui, `MauiUIApplicationDelegate.cs`: https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/MauiUIApplicationDelegate.cs
- dotnet/maui, `MauiApplication.cs` (Android): https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/Android/MauiApplication.cs
- Android, WorkManager *Define work*: https://developer.android.com/develop/background-work/background-tasks/persistent/getting-started/define-work
- Android, *Support for long-running workers*: https://developer.android.com/develop/background-work/background-tasks/persistent/how-to/long-running
- Android, *Persistent work*: https://developer.android.com/develop/background-work/background-tasks/persistent
- Android, *Broadcasts* (`goAsync`, under 10 s): https://developer.android.com/develop/background-work/background-tasks/broadcasts
- Android, `JobInfo.Builder`: https://developer.android.com/reference/android/app/job/JobInfo.Builder
- NuGet, `Xamarin.AndroidX.Work.Runtime` 2.11.2.1 (nuspec-beroenden): https://www.nuget.org/packages/Xamarin.AndroidX.Work.Runtime
- Microsoft, *Using background tasks in Windows apps* (lästa via källan i MicrosoftDocs/windows-dev-docs, eftersom learn.microsoft.com var spärrad från containern): https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/background-tasks
