# Sökning i header-baren (förstudie, rev 1)

**Status:** Förstudie. Inget implementerat. Issue: [#307](https://github.com/jonatansoderberg/Maui.Spine/issues/307), prioriterad som P2 i [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317), beroende av [#269](https://github.com/jonatansoderberg/Maui.Spine/issues/269).
**Fråga:** Ska sökfältet i en Spine-sida ritas av Spine (som `HeaderBarView`) eller vara UIKit:s eget (`UISearchController`), givet att iOS 26 flyttat sökningen till skärmens nederkant, och hur deklarerar en sida sökning?
**Svar:** Spine ritar det. `UISearchController` hamnar i verktygsraden längst ner bara när dess `UINavigationItem` sitter i en `UINavigationController` med verktygsrad. En Spine-app har ingen sådan: alla sidor bor i en MAUI-`ContentPage`, och header-baren är en MAUI-`Grid`. Den nativa vägen skulle kräva att Spine gömmer en `UINavigationController` bakom sin egen header-bar och byter `searchController` vid varje virtuell navigering. Det är mer kod och mer risk än att rita fältet själv. Spine ritar i stället en glaskapsel (`Material.Kind="Glass"` från #300) runt MAUI:s `SearchBar`, som redan ger plattformens returtangent och rensa-knapp på iOS, Android och Windows. Placeringen följer HIG: längst ner på iPhone med iOS 26 när nederkanten är ledig, som en sökknapp i header-baren när en flikrad eller en footer tar nederkanten, och överst på alla andra plattformar. Sidan deklarerar sökningen som den deklarerar page actions, med ett attribut på en egenskap i vy-modellen. Sökfliken i iOS 26:s flikrad (`UISearchTab`) blir en egen issue, eftersom MAUI:s `TabbedPage` bygger flikarna med `ViewControllers` och inte med `UITab`.

---

## 1. Slutsatsen i korthet

| Fråga | Svar | Belägg |
|---|---|---|
| Spine-ritat eller `UISearchController`? | **Spine-ritat.** Verktygsradsplaceringen kräver att navigeringsraden tillhör en `UINavigationController` ("On iPhone, when the navigation bar belongs to a UINavigationController, the search bar may be integrated into the toolbar"). Spine har ingen: `SpineHostPage` och `SpineTabPage` är `ContentPage`, och ingen `UINavigationController` förekommer i paketet. | Apple-dok. `SearchBarPlacement.integrated`; `SpineHostPage.cs:13`, `SpineTabPage.cs:8`; §3.1, §4 |
| Var hamnar fältet på iOS 26? | **Nederst** för en regionsida utan flikrad och utan footer: en glaskapsel som vilar ovanför hemindikatorn och följer med upp ovanför tangentbordet när den fokuseras. **I header-baren som en sökknapp** när nederkanten är upptagen (flikrad, footer). Knappen expanderar till ett fält ovanför tangentbordet, som HIG beskriver för en sökknapp i den övre verktygsraden. **Överst** i sheets. | HIG *Search fields*, §5 |
| Och på övriga plattformar? | **Överst**, i en rad under header-baren: iOS 15–18 som `UISearchController` i läget *stacked*, Android som Material 3:s sökfält och iPad/Mac Catalyst längst ut till höger i header-baren. På Windows hamnar fältet i MAUI:s `TitleBar.Content`, som en `AutoSuggestBox`. | §3, §8 |
| Vilken kontroll ligger i fältet? | **MAUI:s `SearchBar`.** Den är `UISearchBar` på iOS, `SearchView` på Android och `AutoSuggestBox` på Windows. Returtangenten "Sök", rensa-knappen och submit (`SearchButtonClicked`/`QuerySubmitted`) kommer från den. | `SearchBarHandler.iOS.cs:18`, `.Android.cs:25`, `.Windows.cs:12` (MAUI `main`) |
| Hur deklarerar en sida sökning? | **`[PageSearch]` på en `string`-egenskap i vy-modellen**, på samma sätt som `[PageAction]` sitter på ett kommando. Spine skapar då ett observerbart `PageSearch` på `ViewModelBase.Search`, som sidan kan ändra medan den visas. Issuens förslag med `ISearchable` och `[NavigableRegion(Search = true)]` delar upp samma sak på två ställen, sidklassen och vy-modellen. | `PageActionDiscovery.cs:13`, §6 |
| Tangentbordet? | Ett fält längst ner **är** #269-problemet. Spine måste veta tangentbordets höjd för att lyfta fältet, och resultatlistan måste få samma inset. Observatören på ramverksnivå som #269 föreslår levereras därför först, och sökningen bygger på den. | §7 |
| `UISearchTab` (sökfliken i iOS 26)? | **Egen issue.** Den kräver `UITabBarController.Tabs` (`UITab`-API:t), men MAUI:s `TabbedRenderer` sätter `ViewControllers`. Det är en sökning för hela appen, inte för en enskild sida. | `TabbedRenderer.cs:383` (MAUI), `SpineTabbedHostPage.Apple.cs:66` |
| Finns API:erna i .NET? | **Ja**, i Microsoft.iOS 26.2 (taggen `dotnet-10.0.1xx-xcode26.2-10217`): `SearchBarPlacementAllowsToolbarIntegration`, `SearchBarPlacementBarButtonItem`, `UINavigationItemSearchBarPlacement.IntegratedButton`, `UISearchTab.AutomaticallyActivatesSearch`, `UITabBarController.BottomAccessory` och `UIView.KeyboardLayoutGuide`. | `src/uikit.cs` i dotnet/macios, §3.1 |

---

## 2. Vad som redan finns i Spine

| Del | Var | Vad det betyder för sökning |
|---|---|---|
| Header-baren är en MAUI-vy | `HeaderBarView.cs:12`, en `Grid` med fem kolumner och två `PageActionView` (`:422–445`), lagd ovanpå sidan i `NavigationRegion.cs:90–110` | Det finns ingen `UINavigationBar` och inget `UINavigationItem` att hänga en `searchController` på. Placering och animation måste komma från Spine, precis som issuen säger. |
| En action per sida av baren | `NavigationRegionViewModel.cs:155` (`PrimaryPageAction`), `:183` (`SecondaryPageAction`) | En sökknapp i header-baren konkurrerar om den högra platsen. Antingen får baren en tredje plats, eller så går sökknappen före sidans egna actions (§9 punkt 5). |
| Titelraden hör till sidan | `PagePresenter.cs:66–68` (rader: titel, innehåll, footer), `ApplyTitleRowHeight` `:428` | En sökrad under titeln på Android, iOS 15–18 och i sheets blir en fjärde rad i samma presenter. Den flyttar med sidan under övergången, så som titeln gör. |
| Footer-plats nederst | `SpinePage.Footer` (`SpinePage.cs:90`), `_footerHost` (`PagePresenter.cs:98–100`) | Den kapsel som vilar nederst på iOS 26 bor bredvid footern. En sida med footer får sökknappen i stället (§5). |
| Säkerområdeskontraktet | `NavigationRegion.cs:64` (`SafeAreaEdges = None`), `ApplySafeAreaPadding` `:235`, `SafeAreaInsetsFor` `:272` | Sökraden och kapseln måste räknas in i `SafeAreaInsets`, så som `BarHeight` räknas in under en flytande header. |
| Scrollkällan och scroll edge | `HeaderBar.ScrollSource` (`HeaderBar.cs:31`), `UIScrollEdgeElementContainerInteraction` (`PagePresenter.Apple.cs:83`) | Samma mekanism med kant `Bottom` ger iOS 26:s mjuka kant bakom kapseln nederst. Listan får `ScrollInset` `Bottom`. |
| Glasytor | `MaterialKind.Glass` (`Material.cs:20`), `UIGlassEffect` och kapselhörn (`MaterialExtensions.Apple.cs:176–190`, `:405`) | Kapseln runt fältet behöver ingen ny kod för glaset. |
| Glasknappar i baren | `PageActionView.UseGlassHeaderActions` (`PageActionView.cs:93`), `SpineOptions.Apple.GlassHeaderActions`/`MorphHeaderActions` (`SpineOptions.cs:347`, `:355`) | Sökknappen är en vanlig glas-page-action med förstoringsglaset. Stäng-knappen bredvid ett aktivt fält är en glascirkel av samma slag. |
| Header-marginal och höjd | `HeaderBarConstants.PageMargin` (`:27`), `BarHeight` (`:184`) | Kapselns sidmarginal och sökradens höjd ska komma därifrån och inte vara nya siffror. |
| Menyer | `PageAction.Menu`, `MenuPicker` (#318, `MenuButton.cs`) | Sökomfång (*scopes*) kan byggas som en menyknapp bredvid fältet i stället för med UIKit:s scope bar (§9 punkt 4). |
| Windows titelrad | `SpineApplication.Windows.cs:77–86`: MAUI:s `TitleBar` med `LeadingContent`/`TrailingContent` | `TitleBar.Content`, mittplatsen, är oanvänd. Där lägger Windows-appar sin sökruta. |
| Tangentbord | Bara i Androids sheet: `SoftInput.AdjustResize` (`BottomSheetPageExtensions.Android.cs:155`) och IME-insets (`:616`). På iOS finns inget. | Det är #269. |
| Flikraden | `SpineTabbedHostPage : TabbedPage`. På iOS är det MAUI:s `UITabBarController` (`SpineTabbedHostPage.Apple.cs:14`), styrd genom `ViewControllers` (`:66`, `:88`) och med `TabBarMinimizeBehavior` (`:41–42`). | En sökflik kräver `UITab`-API:t (§3.1, §4 C). |

Det som saknas är alltså fältet, dess placering och tangentbordet. Sidstrukturen, glaset och scroll edge-effekten finns redan.

---

## 3. Plattformarnas byggstenar

### 3.1 iOS 26 (iPhone)

HIG (*Search fields*, iOS) nämner tre platser: **som flik i flikraden**, **i en verktygsrad nederst eller överst**, och **inline med innehållet**. Om verktygsraden står det: "Place search at the bottom if there's room … Place search at the top when … there's no bottom toolbar." En sökknapp i den övre raden "animates into a search field that appears either above the keyboard or at the top if there isn't space at the bottom". Ett aktivt fält hamnar alltså ovanför tangentbordet i båda fallen. Det som skiljer är var det vilar när det inte används.

UIKit:s väg dit:

- `navigationItem.searchController` med `preferredSearchBarPlacement = .integrated` (nytt i iOS 26, samma råvärde som `.inline`). Apple: "On iPhone, when the navigation bar belongs to a UINavigationController, the search bar may be integrated into the toolbar."
- `searchBarPlacementAllowsToolbarIntegration` (standard `true`) och `searchBarPlacementBarButtonItem`, som anger var i `toolbarItems` fältet ska stå. Utan den hamnar fältet längst till höger i `UIToolbar`.
- `.integratedButton` visar alltid sökningen som en knapp tills den aktiveras.
- `UISearchTab` (iOS 18) med `automaticallyActivatesSearch` (iOS 26): fliken ligger för sig själv längst till höger i flikraden och expanderar till ett sökfält. Enligt WWDC25-session 284 kollapsar de andra flikarna samtidigt. När sökningen avbryts återställs den flik som var vald före.
- `UITabBarController.bottomAccessory` (`UITabAccessory`) är en vy ovanför flikraden. Den är tänkt för t.ex. en spelare, inte för sökning, och föreslås inte här.

Alla namn ovan finns i Microsoft.iOS 26.2 (se §1). **Inget av det sitter i en Spine-app:** det finns ingen `UINavigationController`, och flikraden byggs av MAUI utan `UITab`.

Forumtråd 797701 samlar fyra buggar i `UISearchController` på iOS 26 som gäller placeringarna `integrated`/`integratedButton` och scope-knappar. Enligt tråden fanns de kvar i iOS 26.5 och Apple har inte svarat där. Det är ett skäl till att inte binda Spines sökning till den vägen.

### 3.2 iOS 15–18

Det gamla mönstret är `UISearchController` i läget *stacked*: ett fält under titeln som glider undan när listan scrollas (`hidesSearchBarWhenScrolling`). Det kräver också en `UINavigationBar`. Spine ritar motsvarigheten som en rad under titelraden med en vanlig `UISearchBar`. Spines minimum är iOS 15 (`Directory.Build.props:22`).

### 3.3 iPad och Mac Catalyst

HIG: "Put a search field at the trailing side of the toolbar." Mac-mönstret är ett fält längst till höger i verktygsraden, och på iPad är det detsamma. Spine lägger fältet längst ut till höger i header-baren. Det tar då den högra action-platsen, eller står till vänster om den om den får plats. ⌘F för att fokusera fältet kräver ett `UIKeyCommand` på värdens view controller. Det finns inte i Spine i dag och ingår inte i v1. `searchBarPlacementAllowsExternalIntegration` gäller bara inuti en `UISplitViewController` och är inte aktuellt.

### 3.4 Android

Material 3 har `SearchBar` ("a persistent and prominent search field at the top of the screen") och `SearchView` (en helskärmsvy som expanderar ur `SearchBar`, med plats för historik och förslag). Båda kräver Material Components 1.8 eller senare, och MAUI 10 har 1.12. De är byggda för en `CoordinatorLayout`/`AppBarLayout`, eller för `SearchView.setUpWithSearchBar` utan en sådan. Spines header-bar är ingetdera. Förslaget är därför samma som för iOS 15–18: en Spine-ritad kapsel i M3-form överst (full rundning, ytfärgen *surface container high*) med MAUI:s `SearchView` inuti. Då går returtangenten genom `ImeAction.Search`. Att expandera till helskärm, som `SearchView` gör, är ett senare steg om förslagslistan kräver det.

### 3.5 Windows

MAUI:s `SearchBar` är redan en `AutoSuggestBox`, med `QuerySubmitted`. Förslag (`ItemsSource`, `SuggestionChosen`) exponeras inte av MAUI och sätts av Spine via en handler-mappning, samma form som menyerna i #318. Platsen är `TitleBar.Content` på skrivbordet, mitt i titelraden som i Utforskaren och Inställningar. Spine sätter redan `LeadingContent`/`TrailingContent` där.

---

## 4. Alternativen

| | A. Spine-ritat fält (MAUI `SearchBar` i glaskapsel) | B. `UISearchController` i en dold `UINavigationController` | C. `UISearchTab` i flikraden | D. Fältet i `bottomAccessory` |
|---|---|---|---|---|
| Nederst på iOS 26 | Ja, Spine placerar det | Kanske. Kräver `UINavigationController` + synlig `UIToolbar` men dold navigeringsrad. Om UIKit integrerar fältet i verktygsraden när navigeringsraden är dold är **inte känt**. | Ja, men bara som appens sökflik | Ja, ovanför flikraden |
| Följer Spines navigering | Ja: fältet hör till sidan och följer med i övergången | Nej: ett `navigationItem` för hela värdsidan, som byts vid varje virtuell push och inte animeras i takt med Spines övergång | Ej tillämpligt | Nej: ett för hela flikvärden |
| Sheets | Ja | Nej: sheeten är en egen MAUI-region | Nej | Nej |
| iOS 15–18, Android, Windows | Samma API och kod, med annan placering | Bara iOS 26. Allt annat måste byggas ändå. | Bara iOS | Bara iOS 26 |
| Tangentbord | Spine lyfter fältet (#269) | UIKit lyfter det | UIKit | UIKit |
| Risk | Utseendet måste matchas mot det nativa (en spike, som #377) | Kända buggar (forum 797701), och MAUI:s egen säkerområdeshantering kring en ny container | MAUI:s renderare sätter `ViewControllers` och synkar `CurrentPage` via sin delegat. Att byta till `Tabs` sker under den. | Strider mot HIG:s avsikt |

**Rekommendation: A.** Det är samma avvägning som gjordes för header-baren och glasknapparna. Spine äger navigeringen, så Spine måste äga det som flyttar med den. B ger det nativa fältet bara i ett av fem fall, och bara med en dold container som ingen i repot har provat. C är ett bra tillägg men löser en annan fråga: en sökning för hela appen, inte sökning på en sida. D avråds.

---

## 5. Placeringsregeln (`SearchPlacement.Automatic`)

| Sammanhang | iPhone, iOS 26 | iPhone, iOS 15–18 | iPad, Mac Catalyst | Android | Windows |
|---|---|---|---|---|---|
| Regionsida utan flikrad och utan footer | **Kapsel nederst**, full bredd inom `PageMargin`, över innehållet med mjuk kant `Bottom`. Aktiv: ovanför tangentbordet, med en glascirkel för att stänga till höger. | Rad under titeln | Längst ut till höger i header-baren | Kapsel i en rad under header-baren | `TitleBar.Content` (skrivbord), annars rad under header-baren |
| Flik-rotsida eller sida i en flik (flikraden syns) | **Sökknapp** (förstoringsglas) till höger i header-baren. Tryck öppnar fältet ovanför tangentbordet. | Rad under titeln | Som ovan | Som ovan | Som ovan |
| Sida med `Footer` | Sökknapp, som ovan | Rad under titeln | Som ovan | Som ovan | Som ovan |
| Sheet | Rad under header-baren | Rad under header-baren | Rad under header-baren | Rad under header-baren | Rad under header-baren |

`SearchPlacement.Top` tvingar raden under header-baren och `SearchPlacement.Button` tvingar sökknappen, på alla plattformar. Sheets läggs överst eftersom nederkanten flyttar sig med detenterna. På Android ligger sheetens innehåll dessutom förskjutet (`SetSheetOverhang`), vilket ett fält nederst skulle behöva följa. Ett nativt exempel på sök nederst i ett sheet (Kartor) är inte undersökt.

Raden under header-baren räknas in i sidans topp-inset precis som `BarHeight`. Under `HeaderBarMode.Overlay` och `LargeTitle` flyter den med baren. Om raden ska glida undan vid scroll, som *stacked* gör, avgörs i steg 3 och står inte i v1 (§9 punkt 6).

---

## 6. Föreslagen API-yta

Den följer `PageAction`: ett attribut i vy-modellen blir en observerbar modell som sidan kan ändra medan den visas.

```csharp
namespace Plugin.Maui.Spine.Core;

public enum SearchPlacement { Automatic, Top, Button }

/// <summary>Declares the page's search field on the string property that holds its text.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PageSearchAttribute : Attribute
{
    public string? Placeholder { get; init; }
    /// <summary>Name of an ICommand run on the return key or a picked suggestion, with the text as parameter.</summary>
    public string? Submit { get; init; }
    /// <summary>Name of an IEnumerable&lt;string&gt; property shown under the field while it is active.</summary>
    public string? Suggestions { get; init; }
    public SearchPlacement Placement { get; init; }
}

/// <summary>The page's search field, as the header bar shows it. Observable, like PageAction.</summary>
public sealed partial class PageSearch : ObservableObject
{
    [ObservableProperty] public partial string Text { get; set; } = "";
    [ObservableProperty] public partial string? Placeholder { get; set; }
    [ObservableProperty] public partial SearchPlacement Placement { get; set; }
    /// <summary>Whether the field has focus. Set it to open or close search from code.</summary>
    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial bool IsVisible { get; set; } = true;
    [ObservableProperty] public partial IEnumerable<string>? Suggestions { get; set; }
    public ICommand? SubmitCommand { get; init; }
}

// ViewModelBase
public partial PageSearch? Search { get; set; }   // set by discovery, or by hand
```

En sida:

```csharp
public partial class CompetitionsViewModel(ICompetitionService _competitions) : ViewModelBase
{
    [PageSearch(Placeholder = "Sök tävling", Submit = nameof(OpenFirstCommand), Suggestions = nameof(Recent))]
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    public ObservableCollection<string> Recent { get; } = [];

    partial void OnQueryChanged(string value) => Filtered = _competitions.Filter(value);

    [RelayCommand]
    private Task OpenFirst(string text) => /* ... */;
}
```

```xml
<!-- XAML: nothing. The list the page already has is what search filters. -->
<CollectionView ItemsSource="{Binding Filtered}" />
```

Varför så:

- **Texten är vy-modellens egen egenskap.** `partial void OnQueryChanged` är det sätt att reagera som toolkit-appar redan använder. Spine binder fältet tvåvägs mot egenskapen via `PageSearch.Text`. Med issuens `ISearchable` måste vy-modellen i stället heta som interfacet och själv skicka `PropertyChanged` för `Query`.
- **Ett ställe.** `[NavigableRegion(Search = true)]` sitter på sidklassen och `ISearchable` på vy-modellen, alltså två deklarationer för en sak. `[PageAction]` har visat att vy-modellen räcker. Issuens skiss har attributet på vy-modellen, men `NavigableAttribute` sitter på sidan (`NavigableAttribute.cs:28`).
- **Spine ritar inga resultat.** Sidan filtrerar sin egen lista. `UISearchController` byter till en resultatvy (`searchResultsController`), men den modellen passar inte en sida som redan är sin lista, och det gör de tre användningsfallen i issuen (Orientera #94, Puckkoll, Almanacka).
- **Förslag är en lista med strängar** som visas under fältet medan det är aktivt. Ett val sätter texten och kör `Submit`. På Windows går de rakt in i `AutoSuggestBox.ItemsSource`.
- **Utan scopes i v1.** Se §9 punkt 4.
- Discovery sker i `PageActionDiscovery`, eller i en syskonklass, och körs en gång per vy-modell (`DeclaredActionsAdded`-mönstret).

---

## 7. Tangentbordet (#269)

Fältet nederst på iOS 26 är precis det #269 beskriver: Almanacka har sitt namnsdagsfält nederst på iOS, och det hamnade under tangentbordet. Appen löser det i dag med `UIKeyboard.Notifications.ObserveWillChangeFrame` i sidan. Sökningen behöver tre saker som #269 också behöver:

1. **Tangentbordets överlapp i regionens koordinater**, animerat med tangentbordets kurva och längd. På iOS kommer det från `UIKeyboard` frame-notiser. `UIView.KeyboardLayoutGuide` (iOS 15, bunden) är ett alternativ för en nativ vy, men fältet är en MAUI-vy i en MAUI-`Grid`, så notisen med kurvan ligger närmast. På Android kommer det från `WindowInsetsCompat.Type.Ime()` genom den befintliga `SystemInsetsProvider`, med `WindowInsetsAnimationCompat` om fältet ska följa med under animationen.
2. **Kapselns lyft:** `TranslationY = -(overlap - bottomSafeArea)` medan fältet är aktivt.
3. **Listans inset:** botten-`ScrollInset` blir *kapsel + överlapp*, så att sista raden går att nå ovanför både tangentbordet och fältet.

Förslaget är att #269:s ramverksvariant (observatören i `NavigationRegion`, insetet på `ViewModelBase`) levereras först, som steg 1, och att sökningen läser samma värde. Då försvinner Almanackas workaround två gånger om: både genom #269 och genom att fältet blir Spines. På Android, i Windows och med fältet överst är tangentbordet bara listans problem, alltså punkt 3.

---

## 8. Plattformar

| Plattform | Stöd | Byggsten | Anmärkning |
|---|---|---|---|
| iOS 26+ (iPhone) | Fullt | Glaskapsel (`MaterialKind.Glass`) + `UISearchBar` via MAUI, nederst eller som sökknapp | Mjuk kant `Bottom` via `UIScrollEdgeElementContainerInteraction` |
| iOS 15–18 | Fullt | `UISearchBar` via MAUI i en rad under titeln | Utan glas |
| iPadOS | Fullt | Fält längst till höger i header-baren | Kompakt bredd (Split View) som iPhone |
| Mac Catalyst | Fullt | Samma som iPad | ⌘F i ett senare steg |
| Android | Fullt | M3-formad kapsel + `SearchView` via MAUI, överst | M3:s `SearchView` i helskärm är ett möjligt senare steg |
| Windows | Fullt | `AutoSuggestBox` via MAUI i `TitleBar.Content`, med förslag via mappning | Rad under header-baren när titelraden är dold |
| iOS 26 sökflik (`UISearchTab`) | Ingår inte | Egen issue | Kräver `UITab` i stället för MAUI:s `ViewControllers` |

---

## 9. Vad som inte går, och vad som inte är verifierat

Studien är gjord i en Linux-container utan Mac, simulator eller enhet. **Inget är provat.** API-namnen är kontrollerade mot Apples dokumentation och mot `src/uikit.cs` i dotnet/macios vid den tagg repot bygger mot. Placeringsreglerna bygger på HIG, och MAUI-beteendet på källkoden på `main`.

1. **Om UIKit integrerar ett `UISearchController` i verktygsraden när navigeringsraden är dold** är okänt. Det är det enda som kunde göra alternativ B billigare. En kvälls spike i simulatorn avgör det. Studien rekommenderar A oavsett, av skälen i §4.
2. **Hur `UISearchBar` ser ut i iOS 26 utanför en systemrad.** Om den ritar egen glas-bakgrund, eller behöver `SearchBarStyle.Minimal` och en rensad `SearchTextField`-bakgrund inuti Spines kapsel, måste mätas mot en nativ referens (`UINavigationController` + `UIToolbar` + `searchController`) på samma sätt som #377 mätte header-höjden. Kapselns höjd, marginal och stäng-knapp tas från den mätningen, inte ur minnet.
3. **Var UIKit lägger sökningen på iOS 26 när en flikrad syns** (verktygsraden ligger då under flikraden) är inte verifierat. Regeln i §5 (sökknapp i header-baren) följer HIG:s "no bottom toolbar"-fall och kontrolleras i samma referens.
4. **Scopes.** UIKit:s scope bar har tre av de fyra buggarna i forumtråd 797701. Förslaget är att scopes blir en menyknapp (#318, `MenuPicker`) bredvid fältet. Det är ett designbeslut för ägaren och ingår inte i v1.
5. **En tredje plats i header-baren.** Sökknappen tar den högra platsen och sidans egna action försvinner. Det duger för v1 (sidor med sökning har sällan en till action), men om både sök och t.ex. filter ska synas behöver `HeaderBarView` en grupp med två knappar. På iOS 26 är det glasgruppen som `UINavigationBar` ritar för bildknappar. Beslut för ägaren.
6. **Sökrad som glider undan vid scroll** (iOS 15–18, Android) kräver att raden följer `HeaderBarCollapseProgress` och ändrar topp-inset under scroll. Mekanismen finns sedan #330 men är inte provad för en extra rad.
7. **Android-kapselns mått.** M3-specen har måtten, men de är inte kontrollerade mot stilarna i Material 1.12. Mät dem i emulatorn mot en nativ `SearchBar`.
8. **Tangentbordsanimationen på iOS.** Att översätta kurvan (`UIViewAnimationCurve` 7, som inte är publik) till MAUI:s `Easing` är ett känt problem. Är det för grovt kan kapselns lyft behöva göras nativt: en `UIView`-animation med tangentbordets kurva på handlerns plattformsvy.
9. **Mac Catalyst ⌘F och fältet i titelraden** är inte provade. Spine gör i dag titelraden genomskinlig (`SpineApplication.MacCatalyst.cs:92`), och fältet hamnar i header-baren under den.

---

## 10. Leveransplan

1. **#269 först:** tangentbordsobservatören i `NavigationRegion` (iOS-notiser, Android-IME), insetet på `ViewModelBase`, listans botten-inset. Almanackas workaround tas bort.
2. **Spike på iOS 26 (simulator, sedan enhet):** en nativ referens (`UINavigationController` med verktygsrad och `searchController`, med och utan flikrad) bredvid en glaskapsel med MAUI:s `SearchBar`. Mät vilohöjd, marginal och läget ovanför tangentbordet, jämför med skärmbilder och besvara §9 punkt 1–3.
3. **API + överst:** `PageSearchAttribute`, `PageSearch`, `ViewModelBase.Search`, discovery. Raden under header-baren (iOS 15–18, Android, sheets), fältet längst till höger (iPad, Mac Catalyst) och `TitleBar.Content` + förslag (Windows).
4. **iOS 26 nederst:** kapseln, mjuk kant `Bottom`, lyftet ovanför tangentbordet, sökknappen för flik- och footer-sidor, stäng-cirkeln, `IsActive` från kod.
5. **Förslagslistan** på iOS och Android, och ⌘F på Mac Catalyst.
6. **Sample och docs:** en sida "Search" i `MauiSpineSampleApp` (lista som filtreras, förslag, placeringsval), `docs/wiki/search.md`, en rad i `page-actions.md`, skillen `/spine-page`.
7. **I apparna:** Orienteras fritextsökning (#94), Almanackas namnsök, Puckkoll.
8. **Egna issues:** `UISearchTab` (sökflik i iOS 26, kräver `UITab` under MAUI:s `TabbedPage`), scopes via #318 och en grupp med två knappar i header-baren (§9 punkt 5).

---

## 11. Referenser

- Issue #307, *Search in the header bar*: https://github.com/jonatansoderberg/Maui.Spine/issues/307
- Issue #317, prioriterad backlog: https://github.com/jonatansoderberg/Maui.Spine/issues/317
- Issue #269, *Keyboard avoidance*: https://github.com/jonatansoderberg/Maui.Spine/issues/269
- Apple HIG, *Search fields*: https://developer.apple.com/design/human-interface-guidelines/search-fields
- Apple, *Build a UIKit app with the new design* (WWDC25, session 284): https://developer.apple.com/videos/play/wwdc2025/284/
- Apple, *Customizing your app's navigation bar* (avsnittet *Integrate search in your toolbar*): https://developer.apple.com/documentation/uikit/customizing-your-app-s-navigation-bar
- Apple, `UINavigationItem.SearchBarPlacement.integrated`: https://developer.apple.com/documentation/uikit/uinavigationitem/searchbarplacement-swift.enum/integrated
- Apple, `UINavigationItem.SearchBarPlacement.integratedButton`: https://developer.apple.com/documentation/uikit/uinavigationitem/searchbarplacement-swift.enum/integratedbutton
- Apple, `searchBarPlacementAllowsToolbarIntegration`: https://developer.apple.com/documentation/uikit/uinavigationitem/searchbarplacementallowstoolbarintegration
- Apple, `searchBarPlacementBarButtonItem`: https://developer.apple.com/documentation/uikit/uinavigationitem/searchbarplacementbarbuttonitem
- Apple, `searchBarPlacementAllowsExternalIntegration`: https://developer.apple.com/documentation/uikit/uinavigationitem/searchbarplacementallowsexternalintegration
- Apple, `preferredSearchBarPlacement`: https://developer.apple.com/documentation/uikit/uinavigationitem/preferredsearchbarplacement
- Apple, `UISearchTab`: https://developer.apple.com/documentation/uikit/uisearchtab
- Apple, `UISearchTab.automaticallyActivatesSearch`: https://developer.apple.com/documentation/uikit/uisearchtab/automaticallyactivatessearch
- Apple, `UITabBarController.bottomAccessory`: https://developer.apple.com/documentation/uikit/uitabbarcontroller/bottomaccessory
- Apple-forum 797701, *Summary of iOS/iPadOS 26 UIKit bugs related to UISearchController & UISearchBar using scope buttons*: https://developer.apple.com/forums/thread/797701
- dotnet/macios, `src/uikit.cs` vid `dotnet-10.0.1xx-xcode26.2-10217`: https://github.com/dotnet/macios/blob/dotnet-10.0.1xx-xcode26.2-10217/src/uikit.cs
- MAUI, `TabbedRenderer.cs` (iOS) på `main`: https://github.com/dotnet/maui/blob/main/src/Controls/src/Core/Compatibility/Handlers/TabbedPage/iOS/TabbedRenderer.cs
- MAUI, `SearchBarHandler.iOS.cs`, `.Android.cs`, `.Windows.cs` på `main`: https://github.com/dotnet/maui/tree/main/src/Core/src/Handlers/SearchBar
- MAUI, `TitleBar.cs` på `main`: https://github.com/dotnet/maui/blob/main/src/Controls/src/Core/TitleBar/TitleBar.cs
- Material Components Android, *Search*: https://github.com/material-components/material-components-android/blob/master/docs/components/Search.md
- Spine: `issues/330-collapse-on-scroll.md`, `issues/366-scroll-edge.md`, `issues/377-ios26-header-height.md`, `issues/379-header-bar-page.md`, `issues/409-…`, `issues/411-…`, `issues/318-menu-buttons.md`, `docs/wiki/regions.md`, `docs/wiki/page-actions.md`, `docs/wiki/tab-host.md`, `docs/proposals/spine-glass-buttons.md`
