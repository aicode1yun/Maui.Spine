# Delade element och zoom mellan sidor i Spine (förstudie, rev 1)

**Status:** Förstudie, med ägarens beslut från 2026-09-30 i avsnittet [Beslut](#beslut-2026-09-30). Inget implementerat; spiken återstår. Issue: [#304](https://github.com/jonatansoderberg/Maui.Spine/issues/304) (P1 i backloggen [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317), som föreslår en spik för just den här idén eftersom den bär mest designrisk). Studien är gjord i en Linux-container utan Mac, Windows eller enhet: Spines kod, plattformsdokumentationen och bindningarna är lästa, inget är kört.
**Fråga:** Kan Spine ge delade element (en bild eller ett kort som flyger från listan till detaljsidan) och zoom (sidan växer ur det tryckta kortet och krymper tillbaka med back-swipen) mellan sidor — med ett attribut på käll- och målvyn, utan animationskod i appen — och kan plattformarnas egna API:er (iOS 18 `.zoom`, Androids shared elements och container transform, Windows `ConnectedAnimationService`) göra jobbet?
**Svar:** Ja, men för regionsidor måste Spine rita övergången själv. Spines stack är virtuell: alla sidor i en region är MAUI-vyer i samma `ContentPage`, i två `ContentView`-värdar, så det finns ingen `UIViewController`, inget `Fragment` och ingen `Frame`-navigering per sida att hänga `.zoom` eller fragmentövergångar på. Förslaget är en attached property `Hero.Tag`, ett överläggslager i `NavigationRegion` som flyger en ögonblicksbild av källan till målet (och zoomar hela sidan när taggen sitter på målsidans rot), samma koreografi på alla plattformar, driven av den back-swipe som redan finns — och på Android av den prediktiva bakåtgestens förlopp. Native används där Spine redan har en native presentation: iOS 18+ `.zoom` på arkets `UIViewController`, och på den helskärmspresentation som lightboxen ([#311](https://github.com/jonatansoderberg/Maui.Spine/issues/311)) behöver. Windows kan använda `ConnectedAnimationService` som motor för flygningen, eftersom den arbetar på vynivå.

---

## 1. Slutsatsen i korthet

| Fråga | Svar | Belägg |
|---|---|---|
| Kan iOS 18 `.zoom` användas för Spines push? | **Nej.** `preferredTransition` verkar vid push i en `UINavigationController` och vid modal presentation. En regionsida är ingen view controller; den byts in i `NavigationRegion`s framvärd. | Apple, *Enhancing your app with fluid transitions*; WWDC24 10145; `NavigationRegionViewModel.cs:203-243`, §2, §3.1 |
| Kan `.zoom` användas någonstans i Spine? | **Ja, för ark.** Spines ark på iOS är en riktig `UIViewController` som presenteras modalt (`PresentViewController`), och Apple säger att samma API fungerar för sheet- och helskärmspresentationer. Det är också vägen för lightboxen. | `BottomSheetPageExtensions.Apple.cs:90-139`; WWDC24 10145, §8 |
| Android: fragmentövergångar? | **Nej**, Spine har inga fragment per sida. `MaterialContainerTransform` fungerar däremot mellan två vyer i samma aktivitet, men den går inte att styra med en gest (`isSeekingSupported` saknas, standard `false`) och kan därför inte följa prediktiv bakåt. | Material *Motion.md* ("Transition between Views"); `MaterialContainerTransform.java`; AndroidX `Transition.isSeekingSupported`, §3.2 |
| Windows: `ConnectedAnimationService`? | **Ja, som motor.** Den tar två godtyckliga `UIElement` (`PrepareToAnimate` på källan, `TryStart` på målet) och kräver ingen `Frame`; WinUI 3 Gallery använder den inom en sida. Ingen gest att styra den med, men Windows har ingen back-swipe att följa. | windows-dev-docs *connected-animation.md*; WinUI-Gallery `CardPage.xaml.cs`, §3.3 |
| Vem ritar alltså? | **Spine**, i `NavigationRegion`, som äger båda värdarna, header-baren och back-swipen. Inte `PagePresenter` som issuen skissar: den håller en sida, inte två. | `NavigationRegion.cs:68-127`, §5 |
| Hur märks käll- och målvy? | `Hero.Tag` på båda, bunden till samma id. Taggen på målsidans **rot** betyder zoom; på en vy **inuti** målsidan betyder den delat element. Ingen ny parameter på `NavigateToAsync`. | §5.1 |
| Interaktivt? | **Ja på iOS** via den befintliga kant-pannan (`NavigationRegion.cs:496-601`). **Android** kräver att Spines bakåt-callback också tar `HandleOnBackProgressed`; AndroidX Activity 1.10 som MAUI 10.0.50 pinnar har det. | §6 |
| Återanvända celler? | Går, om uppslaget görs per tagg vid varje övergång och aldrig håller en cell kvar. Scrollad ur bild ⇒ vanlig bakåtövergång. | §7 |
| Reduce Motion | Tonar i stället för att flyga. `ReducedMotion.IsOn` finns redan; `DefaultSpineTransitions` läser den inte i dag. | `ReducedMotion.cs`, `PageActionView.cs:104` |

---

## 2. Hur Spine navigerar i dag

### 2.1 En region är två värdar och en header

`NavigationRegion` (`Presentation/NavigationRegion.cs`) är en `ContentView` med en `Grid` i fyra lager, i den här ordningen: `_contentHostBack`, ett mörkläggningslager för back-swipen, `_contentHostFront` och `HeaderBarView` överst (`NavigationRegion.cs:68-110`). Värdarna binds till `NavigationRegionViewModel.BackView`/`FrontView`, två `PagePresenter` (en `Grid` med sidan, titelraden och en eventuell footer, `PagePresenter.cs:13-100`).

Push (`NavigationRegionViewModel.cs:203-243`): den nya sidan läggs i `FrontView`, den gamla i `BackView`, header-bindningarna uppdateras, och sedan körs `ISpineTransitions.AnimateNavigateToShowAsync(FrontView)` och `AnimateNavigateToHideAsync(BackView)` **direkt** (rad 229) — innan den nya sidan har layoutats. Därefter lossas den gamla sidan ur trädet (`BackView.Content = null`, rad 231). Pop (`BackAsync`, rad 280-341) gör samma sak baklänges och hänger tillbaka föregående sida i `BackView` först när den ska visas.

`ISpineTransitions` (`Presentation/ISpineTransitions.cs`) ser bara en `View` per anrop (presentern), aldrig vilken sida som lämnas, vilken som kommer eller varifrån användaren tryckte. Standardimplementationen glider i sidled, 300 ms (250 på Windows), och registreras som singleton (`MauiAppBuilderExtensions.cs:53`).

`NavigationService` levererar navigationsparametern **innan** sidan pushas (`NavigationService.cs:100-107`): en målvys `Hero.Tag` bunden till ett id som vymodellen sätter i `OnNavigationParameterAsync` är alltså redan satt när övergången börjar. Det är en förutsättning för §5.

### 2.2 Back-swipen

Kant-pannan är en MAUI-`PanGestureRecognizer` på framvärden (`NavigationRegion.cs:114-121`). Tryck inom vänstra 25 % accepterar gesten (`OnPointerPressed`, rad 402-412); på iOS och Mac Catalyst får den inbyggda `UIPanGestureRecognizer` en `ShouldBegin` som bara släpper igenom ett högerdrag som är mer horisontellt än vertikalt (`NavigationRegion.Apple.cs:21-40`, issue #267). Under draget flyttas framvärden med fingret och föregående sida avslöjas med ett klipp (`ApplyBackReveal`, rad 414-445); över en tredjedel av bredden fullbordas pop, annars fjädrar sidan tillbaka (rad 559-599). `StartInteractiveBack` hänger in föregående sida i `BackView` när draget börjar (`NavigationRegionViewModel.cs:423-455`).

På Android går systemets bakåt till en `OnBackPressedCallback` som bara implementerar `HandleOnBackPressed` (`SpineApplication.Android.cs:167-180`): ingenting rör sig under gesten, sidan glider först när den släpps.

### 2.3 Ark och flikar

- **iOS/Mac Catalyst:** arket är `SpineSheetViewController`, presenterat som `PageSheet` med `ModalInPresentation = true` så att varje dra-för-att-stänga går via delegaten och sidans vakt (`BottomSheetPageExtensions.Apple.cs:90-139`).
- **Android:** `BottomSheetDialog`, alltså ett eget fönster (`BottomSheetPageExtensions.Android.cs:114`, 486).
- **Windows:** en WinUI-popup i samma fönster (`BottomSheetPageExtensions.Windows.cs:40-70`).
- **Flikar:** `SpineTabbedHostPage` har en `NavigationRegion` per flik; flikbyten går inte genom `ISpineTransitions` (docs/proposals/spine-tab-host.md).

### 2.4 Vad det betyder för #304

Ingen plattform ser Spines sidbyten som sidbyten. Det är det som gör idén möjlig — Spine har båda sidorna i handen samtidigt, i samma `Grid` — och det är samma sak som stänger plattformarnas navigationsövergångar ute. Och två saker i dagens pipeline måste ändras för att en flygning ska landa rätt: målsidan måste hinna layoutas innan animationen startar, och föregående sida måste hänga i trädet och vara layoutad innan en tillbakaflygning kan veta var den ska landa.

---

## 3. Plattformarnas byggstenar

### 3.1 iOS och Mac Catalyst

- `UIViewController.preferredTransition` och `UIViewController.Transition.zoom(options:sourceViewProvider:)`, iOS/iPadOS/Mac Catalyst 18.0. Källvyns provider anropas både när sidan presenteras och när den stängs; Apple rekommenderar att slå upp vyn med ett stabilt id i stället för att hålla en `UIView` eller `IndexPath`.
- Gäller push i en `UINavigationController` och, enligt WWDC24 10145, "the fullScreenCover and sheet presentation APIs in both SwiftUI and UIKit". Övergången är kontinuerligt interaktiv: den kan gripas och dras mitt i, och en avbruten push blir en pop.
- *"The fluid zoom transitions are available on iPhone and iPad … The API is available on other platforms, but the system uses the default transitions."* Mac Catalyst får alltså sannolikt inte zoomen, fast API:et finns där; issuens "Full (to verify)" är enligt dokumentationen snarare "nej". Om Catalyst i iPad-idiom räknas som iPad säger dokumentationen inget om.
- `ZoomOptions`: `interactiveDismissShouldBegin`, `alignmentRectProvider`, `dimmingColor`, `dimmingVisualEffect`. iOS 26 lade till `zoom(options:sourceBarButtonItemProvider:)` (arket växer ur en knapp i navigationsfältet).
- Bindningen finns i .NET: `UIViewController.PreferredTransition`, `UIViewControllerTransition.Zoom(UIZoomTransitionOptions, Func<UIZoomTransitionSourceViewProviderContext, UIView>)` och `UIZoomTransitionOptions.InteractiveDismissShouldBegin` i `dotnet/macios` `src/uikit.cs` (main). Inte grep:at i repots Microsoft.iOS 26.2-paket — containern har inga workloads.
- Känt problem: zoom in i ett ark med medium-detent ger ett ryck i dimningen efter övergången (Apple-forum 788257, SwiftUI, obesvarat).
- Utanför zoom finns inget vynivå-API för delade element i UIKit. Egna övergångar byggs med `snapshotView(afterScreenUpdates:)` och `UIViewPropertyAnimator` — samma byggstenar Spine skulle använda.

### 3.2 Android

- **Fragment shared elements** kräver fragment per sida. Spines sidor är vyer i MAUI:s enda aktivitet (i flikvärden ligger flikarna i fragment, men sidorna inuti en flik gör det inte). Inte tillämpligt.
- **`MaterialContainerTransform` mellan vyer:** sätt `startView`/`endView`, `TransitionManager.beginDelayedTransition(container, transform)`. Det är dokumenterat och passar Spines struktur. Material 1.12.0.5 pinnas av MAUI 10.0.50 (`eng/AndroidX.targets`), som repot bygger med (`Directory.Packages.props:10`). Men klassen överskuggar inte `isSeekingSupported`, som i AndroidX `Transition` returnerar `false`: den kan inte skrubbas av en gest.
- **Prediktiv bakåt:** `OnBackPressedCallback.HandleOnBackStarted/HandleOnBackProgressed/HandleOnBackCancelled` med `BackEventCompat.Progress` kräver AndroidX Activity 1.8; MAUI 10.0.50 pinnar 1.10.1.3. Skrubbningsbara övergångar (`TransitionManager.controlDelayedTransition`) kräver AndroidX Transition 1.5; MAUI pinnar 1.6.0.1. För appar med targetSdk 36 på Android 16 är prediktiv bakåt påslaget som standard och `onBackPressed` anropas inte längre; Spine använder redan dispatcher-callbacken och påverkas inte av det.
- **Bilder:** MAUI laddar bilder med Glide (`src/Core/AndroidNative/maui/.../glide`). Om Glide levererar hardware bitmaps kan de inte ritas i en mjukvaru-`Canvas` — en ögonblicksbild via `View.Draw` kan då kasta. Ej verifierat; se §10.

### 3.3 Windows

- `ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(key, source)` på källan, `GetAnimation(key).TryStart(destination)` på målet. Mellan stegen "appears frozen above other UI"; startas animationen inte inom tre sekunder kastas den, och Microsoft råder att inte vänta mer än ~250 ms. Konfigurationerna `Gravity` (framåt, standard) och `Direct` (bakåt, 150 ms), och "coordinated elements" som följer med flygningen.
- Dokumentationen säger att den "can be applied to any experience where you are changing content in a UI"; WinUI 3 Gallery kör den inom en sida (`CardPage.xaml.cs`). Microsofts lista över WinRT-API:er som inte stöds i skrivbordsappar nämner `ConnectedAnimationService` — men den länkar `Windows.UI.Xaml`-varianten; MAUI använder `Microsoft.UI.Xaml`, vars dokumentation och Gallery visar att den fungerar. Oprövat i Spine.

---

## 4. Alternativen

| | A. Plattformarnas navigation | B. Vynivå-API:er per plattform | C. Spine ritar (rekommenderas för regioner) | D. iOS `.zoom` på presentationer (rekommenderas för ark/lightbox) |
|---|---|---|---|---|
| Vad | `UINavigationController`, fragment per sida, `Frame.Navigate` | iOS: inget; Android: `MaterialContainerTransform` vy→vy; Windows: `ConnectedAnimation` | Ögonblicksbild + överlägg + koreografi i `NavigationRegion` | `sheetVc.PreferredTransition = Zoom(...)` |
| Kräver | Att Spine byter navigationsmodell | Tre implementationer | Ett nytt lager i regionen, lite plattformskod för snapshot och koordinater | Att sidan redan presenteras som view controller |
| Interaktiv tillbaka | Gratis | Nej på Android (ej skrubbbar), ingen gest på Windows, saknas på iOS | Ja: kant-pannan på iOS, prediktiv bakåt på Android | Gratis (dra ned, kant) |
| Samma utseende överallt | Nej | Nej | Ja | — (bara iOS/iPadOS) |
| Risk | Omskrivning av Spines kärna | Tre beteenden att hålla i synk | Bildfrekvens när målsidan laddar; layouttiming | Samspel med `ModalInPresentation` och detents |

**A** är uteslutet: Spine finns för att äga navigationen, och varje flik, ark och back-swipe bygger på den virtuella stacken. **B** ger tre olika beteenden och ingen interaktiv tillbakagång på Android — det som gör zoomen värd att ha. **C** är det enda som fungerar för region-push på alla plattformar och som kan följa fingret. **D** kostar nästan inget och ger den äkta iOS-zoomen där den är tillgänglig; den är dessutom exakt vad lightboxen behöver.

Windows är ett gränsfall: `ConnectedAnimation` gör internt vad C gör (fryser källan över UI:t och flyger den), med Fluents egen kurva, och Windows har ingen gest som behöver skrubba. Förslaget är att bakom samma lager låta Windows använda den om spiken visar att den startar i tid (§10); annars C.

---

## 5. Föreslagen design

### 5.1 API

```csharp
namespace Plugin.Maui.Spine.Extensions;

/// <summary>Pairs a view on one page with a view on the next, for a shared-element or zoom transition.</summary>
public static class Hero
{
    public static readonly BindableProperty TagProperty =
        BindableProperty.CreateAttached("Tag", typeof(string), typeof(Hero), null,
            propertyChanged: static (b, _, _) => HeroRegistry.Track((VisualElement)b));

    public static string? GetTag(BindableObject view) => (string?)view.GetValue(TagProperty);
    public static void SetTag(BindableObject view, string? value) => view.SetValue(TagProperty, value);
}
```

```xml
<!-- listsidan: en cell -->
<Border Hero.Tag="{Binding Id, StringFormat='competition-{0}'}">
    <Image Source="{Binding Poster}" Hero.Tag="{Binding Id, StringFormat='poster-{0}'}" />
</Border>

<!-- detaljsidan, delat element: bilden i huvudet tar emot affischen -->
<Image Source="{Binding Poster}" Hero.Tag="{Binding Id, StringFormat='poster-{0}'}" />

<!-- detaljsidan, zoom: taggen på sidans rot, så växer hela sidan ur kortet -->
<ContentView x:Class="Orientera.Pages.CompetitionPage"
             Hero.Tag="{Binding Id, StringFormat='competition-{0}'}"> ... </ContentView>
```

Anropet är oförändrat: `await _navigation.NavigateToAsync<CompetitionPage, CompetitionId>(id);`.

- **Taggens plats avgör läget.** Rot ⇒ zoom (Materials "container transform": kortet blir sidan). Vy inuti sidan ⇒ delat element, med resten av sidan intonad i stället för inglidande. Utan matchande tagg ⇒ dagens övergång.
- **Ingen parameter på `NavigateToAsync`.** Issuens skiss `NavigateAsync<…>(id, transition: SpineTransition.Zoom)` skulle behöva läggas på sex metoder i `INavigationService` (`NavigateToAsync`, `ShowAsync` och `NavigateToWithResultAsync`, två överlagringar var), och den kan ändå inte veta *vilken* vy användaren tryckte på — det vet bara taggarna. Om ägaren vill kunna slå av övergången per anrop är en `SpineOptions.Transitions.SharedElements`-växel och `Hero.Tag = null` enklare.
- **Namnet.** "Hero" används redan för `HeroCollectionView` (den kollapsande bildrubriken). Det är ingen konflikt i kod, och den rubriken är det vanligaste *målet* för en delad bild, men det är ägarens val om namnet ska vara `Hero.Tag` (issuen, Flutters term) eller något annat.

### 5.2 Förloppet vid push

1. Innan den nya sidan hängs in: `HeroRegistry` samlar synliga vyer med tagg på den aktuella sidan (bara de som har en handler, `IsVisible`, ligger inom sidans synliga yta).
2. Den nya sidan hängs in som i dag, men med `Opacity = 0`, och regionen väntar på dess första layout (`SizeChanged` på presentern) — högst ett tidsfönster, t.ex. 100 ms; därefter dagens övergång.
3. Matcha taggar mellan de två sidorna. Ingen match ⇒ dagens övergång.
4. Mät båda vyernas rektanglar i regionens koordinater. MAUI:s `GetBoundingBox` är `internal`, så Spine behöver en egen omräkning per plattform (`ConvertRectToView`, `GetLocationInWindow`, `TransformToVisual` — alla används redan i repot, t.ex. `MaterialExtensions.Android.cs:197`).
5. Ta en ögonblicksbild av källan på plattformsnivå och lägg den i ett överläggslager mellan framvärden och header-baren (ett tomt, `InputTransparent` MAUI-lager vars plattformsvy tar emot bilderna; arkets blur-överlägg lägger på samma sätt en native vy över sidan, `BottomSheetPageExtensions.Apple.cs:244-261`). Dölj källan och målet (`Opacity = 0`).
6. Animera: delat element flyger rektangel och hörnradie från källa till mål medan målsidan tonar in; zoom skalar och klipper hela målsidan från källans rektangel medan föregående sida dämpas.
7. Landa: visa målet, ta bort ögonblicksbilden, återställ källans `Opacity` via vyreferensen, inte via taggen (§7).

Pop gör samma sak åt andra hållet. Föregående sida hängs in i `BackView` före animationen (det görs redan, `NavigationRegionViewModel.cs:315`), taggarna slås upp på nytt och regionen väntar på layout innan den vet var flygningen landar.

### 5.3 Bilder, bildförhållande och hörn

En ögonblicksbild av en kvadratisk miniatyr i `AspectFill` saknar de delar som beskurits bort och kan inte bli en bred rubrikbild utan att tonas över till målets bild. Två fall:

- **`Image` med tagg:** överlägget ritar samma `ImageSource` (redan i cachen) med `AspectFill` och animerar ramen — beskärningen öppnar sig som i Bilder-appen. Det är det vanligaste fallet i issuens exempel och det snyggaste.
- **Allt annat (kort, `Border`, text):** två ögonblicksbilder, källa och mål, som tonas över i varandra medan ramen flyger. Hörnradien från källans `StrokeShape` interpoleras mot 0 (eller målets).

Riktlinje för appar: skicka med miniatyrens URL i navigationsparametern, så att målsidans bild har sin källa när övergången börjar och inte väntar på `TaskState`/`StateView` (#302). En tagg som ligger inne i en `StateView` som ännu visar laddningsläget finns inte, och då blir det dagens övergång.

### 5.4 `ISpineTransitions`

Flygningen körs av regionen, inte av `ISpineTransitions`, men sidornas del av en hero-övergång (intoning, dämpning) och tiderna behöver en plats. Två nya medlemmar med standardimplementation (samma teknik som `ResetHiddenViewAsync` redan använder), så att appar med egna övergångar fortsätter kompilera och får Spines hero-beteende:

```csharp
Task AnimateSharedAsync(SharedTransitionContext context) => SpineHeroTransitions.AnimateSharedAsync(context);
Task AnimateZoomAsync(ZoomTransitionContext context) => SpineHeroTransitions.AnimateZoomAsync(context);
```

`context` bär riktning, båda presenterarna, rektanglarna och ett `Progress`-värde för den interaktiva vägen (§6).

---

## 6. Interaktivitet och back-swipe

- **iOS/Mac Catalyst.** Kant-pannan finns och är redan begränsad till högerdrag från kanten (#267). När sidan kom in med zoom ersätts avslöjandeklippet (`ApplyBackReveal`) av zoomens geometri: `progress = deltaX / bredd`, sidan krymper mot källans rektangel och följer fingret, föregående sida lyser upp. Släpp över tröskeln (i dag 0,33, `NavigationRegion.cs:41`) ⇒ fullborda från aktuell position; under ⇒ tillbaka till helskärm. Delat element under draget: flygningen skrubbas med samma `progress`.
- **Dra nedåt för att stänga** (som iOS 18:s zoom) är inte med i v1: det krockar med vertikal scroll och måste börja bara när sidans lista står i toppen. Det är samma avvägning som `interactiveDismissShouldBegin` finns för i UIKit.
- **Android.** `SpineRegionBackCallback` får `HandleOnBackStarted` (hänger in föregående sida, som `StartInteractiveBack`), `HandleOnBackProgressed` (`BackEventCompat.Progress` skrubbar flygningen), `HandleOnBackCancelled` (tillbaka) och `HandleOnBackPressed` (fullborda). Det ger samtidigt en prediktiv bakåtgest för *vanliga* pop — ett större beteendebyte än #304 och värt en egen issue.
- **Windows.** Ingen gest; flygningen är icke-interaktiv.
- **Vakten.** `OnBackRequestedAsync` frågas i dag först när draget är fullbordat (`NavigationRegionViewModel.cs:457-470`); säger den nej fjädrar sidan tillbaka från där den är. För zoom betyder det en sida som hoppar upp från kortets storlek. Spiken ska visa hur det ser ut; ett alternativ är att fråga vakten när draget *börjar*.
- **Avbruten push.** Apple gör en avbruten zoom-push till en pop. Spine behöver bara hindra att en ny navigation startar mitt i en flygning — eller avsluta flygningen direkt — eftersom `NavigateToAsync` i dag inte skyddar sig mot överlappande anrop.

---

## 7. CollectionView-celler som återanvänds

- **Registret håller aldrig en cell.** `HeroRegistry` håller svaga referenser och uppdateras när `Hero.Tag` ändras — vilket händer varje gång en cell binds om till en annan rad. Vid uppslaget kontrolleras att vyns tagg *fortfarande* är den sökta, att den har en handler och att den är synlig.
- **Ögonblicksbilden tas före navigationen.** När listan väl är lossad (`BackView.Content = null`) finns inget att fotografera.
- **Återställ med vyreferens.** Källan döljs under flygningen; återställningen görs på vyn som dolts, i en `finally`, så att en cell som hunnit återanvändas aldrig blir kvar osynlig.
- **På vägen tillbaka** kan raden ha scrollats bort, listan ha laddats om, eller detaljsidan ha bläddrats till en annan post (dess tagg ändrad). Uppslaget görs då på det som *nu* står i målsidans tagg; finns ingen synlig cell med den taggen blir det dagens bakåtövergång. Att scrolla listan till raden först (som Windows `TryStartConnectedAnimationAsync` gör) är en senare förbättring.
- **Lossad och återinhängd lista.** Listsidan lossas ur trädet efter push och hängs in igen vid pop. Att cellerna då står kvar på samma plats med samma handlers är inte verifierat på iOS eller Android (§10).

---

## 8. Ark, flikar och lightbox

- **Flikar.** Varje flik har sin region; flygningar stannar inom den och flikraden står kvar, som i iOS 18:s Bilder. Inget flikbyte animeras med hero.
- **Region → ark, v1: nej.** Arket är ett annat fönster (Android), en annan view controller (iOS) eller en popup (Windows); en Spine-ritad flygning skulle behöva ett överlägg på fönsternivå. Arket öppnas som i dag.
- **Region → ark på iOS 18+, steg 2: native `.zoom`.** Arkets rot har `Hero.Tag`; `DisplayBottomSheet` sätter `sheetVc.PreferredTransition = UIViewControllerTransition.Zoom(options, ctx => källvyns UIView)` före `PresentViewController`, och slår upp källan per tagg varje gång providern anropas. Frågor till spiken: respekterar zoomens dra-för-att-stänga `ModalInPresentation = true` och Spines vakt, och syns forumets dimningsryck med Spines detents?
- **Arkets egen stack.** Sidor som pushas inuti ett ark går genom arkets `NavigationRegion`; där fungerar C precis som i en rotregion.
- **Lightbox (#311), "the lightbox opens from its thumbnail".** Den naturliga formen på iOS är en helskärmspresentation med `.zoom` från miniatyren: öppning, dra-ned-för-att-stänga och tillbakazoom till rätt miniatyr efter bläddring (providerns `ZoomedViewController` säger vilken bild som visas) kommer från UIKit, och `InteractiveDismissShouldBegin` spärrar stängningen medan bilden är inzoomad. På Android och Windows öppnar lightboxen med C:s flygning från miniatyren till helskärm. #304 levererar alltså lightboxens öppning; lightboxen levererar #304:s första helskärmspresentation.

---

## 9. Plattformar

| Plattform | Delat element (region) | Zoom (region) | Interaktiv tillbaka | Ark / lightbox |
|---|---|---|---|---|
| iOS 18+, iPadOS 18+ | C | C | Kant-pannan | Native `.zoom` (steg 2) |
| iOS 15–17 | C | C | Kant-pannan | Arket som i dag; lightbox med C |
| Mac Catalyst | C (ej verifierat) | C (ej verifierat) | Pannan finns, pekplatta ej provad | `.zoom` faller enligt Apple tillbaka till standard; arket som i dag |
| Android 14+ | C | C | Prediktiv bakåt via `HandleOnBackProgressed` | Arket som i dag; lightbox med C |
| Android < 14 | C | C | Ingen förloppsgest; flygning vid släpp | Som ovan |
| Windows | `ConnectedAnimation` eller C (spiken avgör) | C | — | Arket som i dag; lightbox med C |
| Reduce Motion / animationer av | Toning | Toning | Toning | UIKit:s eget beteende (ej verifierat) |

---

## 10. Spiken: vad den måste visa

Mac med Xcode, iPhone (Jonatans iPhone 16 Pro, iOS 26.5) och en Android 14+-enhet; Windows separat. En lista (`CollectionView` med bildkort) och en detaljsida med `HeroCollectionView`-rubrik, i Orientera-lik form.

1. **Layouttiming.** Hur lång tid från `FrontView.Content = next` till att målvyns rektangel går att mäta — med och utan `StateView`, med `HeroCollectionView` som mål? Räcker ett fönster på 100 ms?
2. **Vägen tillbaka.** När listsidan hängs in igen i `BackView`: finns cellen kvar med samma handler och position, eller realiseras den om? Hur länge innan rektangeln stämmer?
3. **Ögonblicksbild.** iOS: `SnapshotView(afterScreenUpdates: false)` på en cell med MAUI-`Image` — rätt innehåll, eller tomt? Android: `View.Draw` till `Bitmap` på en Glide-laddad bild — undantag för hardware bitmap? Fungerar `PixelCopy` i stället?
4. **Bildfrekvens.** Håller en MAUI-tickad animation (`Animation.Commit`, som back-swipen använder) 120 Hz på ProMotion medan målsidans `OnAppearingAsync` laddar data? Jämför med samma flygning i `UIViewPropertyAnimator` respektive `ValueAnimator`. Svaret avgör om koreografin ska ligga i MAUI eller i en tunn plattformsmotor.
5. **Klipp och skala.** Hur dyrt är ett per bildruta uppdaterat `RoundRectangleGeometry`-klipp på en helskärmspresenter (zoomen), på iOS och Android?
6. **Interaktiv zoom.** Kant-pannan skrubbar zoomen; släpp över och under tröskeln; vakten säger nej mitt i.
7. **Prediktiv bakåt.** Kommer `HandleOnBackProgressed` till Spines callback med appens targetSdk och manifest? Påverkas `BottomSheetDialog`s egen bakåt?
8. **Native zoom på arket.** `PreferredTransition` på `SpineSheetViewController`: öppning från en tagg på regionsidan, stängning tillbaka till den, dra-för-att-stänga med `ModalInPresentation = true`, detents medium och large.
9. **Windows.** `PrepareToAnimate`/`TryStart` mellan två MAUI-handlade `FrameworkElement` i samma fönster, inom 250 ms trots väntan på layout.
10. **Header-baren.** Ska flygningen gå över eller under header-baren, och hur ser page actions ut när de byts under en zoom (de tonas redan in och ut, `PageActionView.cs:104-113`)?

---

## 11. Vad som inte går, och vad som inte är verifierat

**Inget i den här studien är provat.** Den gjordes i en Linux-container utan Mac, Windows, simulator eller enhet, och utan .NET-workloads; varken en rad Spine-kod eller ett plattforms-API har körts. Belägget är Spines källkod, Apples, Googles och Microsofts dokumentation och bindningskällorna i `dotnet/macios`, `dotnet/maui` och `material-components-android`.

Går inte, enligt dokumentationen och koden:

- iOS:s `.zoom` för region-push, utan att Spine lägger varje sida i en `UINavigationController`.
- `.zoom` på Mac: Apple faller tillbaka till standardövergången utanför iPhone och iPad.
- `MaterialContainerTransform` som följer den prediktiva bakåtgesten (ej skrubbbar).
- En Spine-ritad flygning från en regionsida in i ett ark på Android (två fönster).

Inte verifierat, och avgörande: allt i §10, särskilt layouttimingen (1, 2), ögonblicksbilderna (3) och bildfrekvensen (4). Dessutom: att bindningarna i §3.1 finns i repots Microsoft.iOS 26.2 (de finns i `main`), att `ConnectedAnimationService` fungerar i MAUI:s WinUI-fönster, att net10.0-android-apparna i repot faktiskt har targetSdk 36, och hur UIKit:s zoom beter sig med Reduce Motion.

---

## 12. Leveransplan

1. **Spiken** (§10) på en scratch-branch, iOS och Android först. Resultatet avgör MAUI- eller plattformsmotor och tidsfönstret för layout.
2. **Issue-med-plan och changelog** (`issues/304-…md`). Ägarens beslut först: taggens plats som lägesväljare (§5.1), namnet, header-bar-ordningen.
3. **Delat element, icke-interaktivt**, iOS och Android: `Hero`, `HeroRegistry`, överläggslagret och väntan på layout i `NavigationRegion`, `Image`-specialfallet, Reduce Motion. Sample: lista → detalj i `MauiSpineSampleApp`.
4. **Zoom** (tagg på målsidans rot) och **interaktiv tillbaka** via kant-pannan på iOS.
5. **Android prediktiv bakåt** i `SpineRegionBackCallback` — för hero och, i en egen issue, för vanliga pop.
6. **iOS 18+ native zoom på ark**, därefter lightboxen (#311) på samma grund.
7. **Windows:** `ConnectedAnimation` eller C, efter mätning på Windows.
8. **Dokumentation:** `docs/wiki/custom-transitions.md` (nya medlemmar), en wiki-sida om `Hero.Tag`, `spine-page`-skillen. Förstudien blir därefter historik.

---

## Beslut (2026-09-30)

Jonatan gick igenom studiens frågor 2026-09-30 och följde rekommendationerna. Rader märkta **Förslag** saknade rekommendation i studien; där står ett förslag med skäl, som gäller tills han säger annat.

- **Motor.** Spine ritar övergången själv för regionsidor (alternativ C). Native `.zoom` (iOS 18+) används bara för ark och lightbox (alternativ D).
- **Läge.** Taggens plats väljer läge: på målsidans rot blir det zoom, på en vy inuti blir det delat element (§5.1).
- **Ingen parameter på `NavigateToAsync`.** Övergången slås av med en brytare i `SpineOptions.Transitions.SharedElements` eller med `Tag = null`.
- **Namnet** — **Förslag.** `Transition.Tag` i stället för `Hero.Tag`. "Hero" betyder redan den kollapsande rubriken i Spine (`HeroCollectionView`), och två betydelser av samma ord i samma XAML blir svårlästa. `Transition` hör ihop med `ISpineTransitions` och täcker både zoom och delat element. Studiens text säger fortfarande `Hero.Tag`.
- **Header-baren** — **Förslag.** Flygningen går under header-baren, som i §5.2 steg 5. Baren flyter över innehållet i vila (överlägg, Liquid Glass), så en bild som landar under den hamnar direkt i sitt slutläge; över baren skulle baren dyka upp ovanpå bilden när ögonblicksbilden tas bort. Spiken bekräftar.
- **`ISpineTransitions`.** Två nya medlemmar med standardimplementation: `AnimateSharedAsync` och `AnimateZoomAsync` (§5.4).
- **Vakten** — **Förslag.** `OnBackRequestedAsync` frågas som i dag, när draget är fullbordat. Vakten är asynkron och får visa en dialog, vilket inte går att vänta på när ett finger just börjat dra. Om spiken visar att en nekad zoom ser fel ut när sidan fjädrar tillbaka, får en sida med vakt den vanliga avslöjande-gesten i stället för zoomens.
- **Android prediktiv bakåt.** Egen issue för vanliga pop, skild från #304.
- **Windows.** `ConnectedAnimationService` om spiken visar att den hinner starta, annars Spines egen flygning.
- **Region till ark.** Inte i v1. Native `.zoom` på iOS 18+ blir steg 2.
- **Dra nedåt för att stänga.** Inte i v1.
- **Ordningen.** Spiken (§10, iOS och Android först) körs före issue-planen.

---

## 13. Referenser

- Apple, *Enhancing your app with fluid transitions*: https://developer.apple.com/documentation/uikit/enhancing-your-app-with-fluid-transitions
- Apple, `UIViewController.preferredTransition`: https://developer.apple.com/documentation/uikit/uiviewcontroller/preferredtransition
- Apple, `UIViewController.Transition`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition
- Apple, `zoom(options:sourceViewProvider:)`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoom(options:sourceviewprovider:)
- Apple, `zoom(options:sourceBarButtonItemProvider:)` (iOS 26): https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoom(options:sourcebarbuttonitemprovider:)
- Apple, `UIViewController.Transition.ZoomOptions`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoomoptions
- Apple, *Enhance your UI animations and transitions* (WWDC24, session 10145): https://developer.apple.com/videos/play/wwdc2024/10145/
- Apple-forum 788257, zoom in i ett ark med medium-detent: https://developer.apple.com/forums/thread/788257
- dotnet/macios, UIKit-bindningarna (`PreferredTransition`, `UIViewControllerTransition.Zoom`, `UIZoomTransitionOptions`): https://github.com/dotnet/macios/blob/main/src/uikit.cs
- Android, *Add support for predictive back animations* (Views, `HandleOnBackProgressed`, `controlDelayedTransition`): https://developer.android.com/guide/navigation/custom-back/support-animations-views
- Android, *Predictive back gesture*: https://developer.android.com/guide/navigation/custom-back/predictive-back-gesture
- Android 16, beteendeändringar (prediktiv bakåt för targetSdk 36): https://developer.android.com/about/versions/16/behavior-changes-16
- Material Components, *Motion* (container transform, "Transition between Views"): https://github.com/material-components/material-components-android/blob/master/docs/theming/Motion.md
- Material Components, `MaterialContainerTransform.java`: https://github.com/material-components/material-components-android/blob/master/lib/java/com/google/android/material/transition/MaterialContainerTransform.java
- Material Components, *Predictive Back*: https://github.com/material-components/material-components-android/blob/master/docs/foundations/PredictiveBack.md
- AndroidX, `Transition.java` (`isSeekingSupported`): https://github.com/androidx/androidx/blob/androidx-main/transition/transition/src/main/java/androidx/transition/Transition.java
- dotnet/maui 10.0.50, AndroidX-versioner: https://github.com/dotnet/maui/blob/10.0.50/eng/AndroidX.targets
- dotnet/maui, Glide-integrationen på Android: https://github.com/dotnet/maui/tree/main/src/Core/AndroidNative/maui/src/main/java/com/microsoft/maui/glide
- dotnet/maui, `GetBoundingBox` (internal): https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/ViewExtensions.cs
- Microsoft, *Connected animation* (källan i windows-dev-docs): https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/motion/connected-animation.md
- Microsoft, WinRT-API:er i skrivbordsappar: https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/desktop/modernize/winrt-api-desktop-app-support.md
- WinUI 3 Gallery, connected animation inom en sida: https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/SampleSupport/SamplePages/CardPage.xaml.cs
- Spine: `docs/wiki/custom-transitions.md`, `docs/wiki/regions.md` (back-swipe), `docs/wiki/sheets.md`, `docs/wiki/tab-host.md`, `docs/proposals/spine-tab-host.md`, `issues/267-back-swipe-pan-vertical-drags.md`, `issues/428-navigation-showasync-go-to-a-page-without-stacking.md`, `issues/60-back-from-sheet.md`
