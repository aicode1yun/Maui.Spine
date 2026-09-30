# Kontextmenyer i Spine — `ContextMenu.Items` på valfri vy (förstudie, rev 1)

**Status:** Förstudie, med ägarens beslut från 2026-09-30 i avsnittet [Beslut](#beslut-2026-09-30). Inget implementerat. Issue: [#306](https://github.com/jonatansoderberg/Maui.Spine/issues/306), prioriterad som P2 i [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317) med frågorna "Sharing the `PageAction` model, preview support".
**Fråga:** Kan Spine ge valfri vy en systemets egen kontextmeny (long-press på pekskärm, högerklick på desktop) med ikoner, destruktiva rader, undermenyer och på iOS en förhandsvisning, deklarerad med samma modell som header-barens menyknappar, och fungera i list-rader?
**Svar:** Ja, men modellen som ska delas är **`MenuItems`, inte `PageAction`**. #318 byggde redan den delade menymodellen (`MenuAction`, `MenuSection`, `SubMenu`, `MenuPicker`) och en menybyggare per plattform. Kontextmenyn blir en attached property, `ContextMenu.Items`, som följer vyns handler på samma sätt som `Tap.Command`, och som på varje plattform återanvänder #318:s byggare: `UIContextMenuInteraction` på iOS och Mac Catalyst, `PopupMenu` vid long-click på Android och `UIElement.ContextFlyout` på Windows. Förhandsvisning finns bara på iOS/iPadOS. v1 lyfter vyn själv, med rundade hörn. En egen förhandsvisning (`ContextMenu.Preview`) är ett andra steg. Android och Windows får ingen förhandsvisning, eftersom plattformarna inte har någon.

---

## 1. Slutsatsen i korthet

| Fråga | Svar | Belägg |
|---|---|---|
| Dela `PageAction`-modellen? | **Nej. Dela `MenuItems`.** `PageAction` är en header-knapp (`Placement`, `Role`, `Badge`, `IsSelected`, `Haptic`) och har varken `IsDestructive`, `IsChecked` eller undermenyer. `MenuAction` har allt det, och en `PageAction` bär redan en `MenuItems` genom `PageAction.Menu`. En kontextmeny och en header-meny kan alltså vara **samma instans**. Issuens skiss `new PageAction("Follow", Icons.Star, FollowCommand)` kompilerar inte mot dagens `PageAction`, som bara har konstruktorn `(text, command)`. | `PageAction.cs:53`, `:76`, `:101`; `MenuElements.cs:24`; `issues/318-menu-buttons.md` ("Shared with the context-menu idea (#306)") |
| Förhandsvisning? | **iOS/iPadOS: ja.** `UIContextMenuInteraction` lyfter vyn (en *targeted preview*) eller visar en egen vy-kontroller från `previewProvider`. **Mac Catalyst: nej.** Mac-menyn är "compact": "A nonmodal, compact menu with no preview". **Android, Windows: nej**, systemet har inget motsvarande. | Apple-dokumentationen för `UIContextMenuInteraction.appearance`, §4 |
| Räcker MAUI:s `FlyoutBase.ContextFlyout`? | **Nej.** Dokumentationen säger "on Mac Catalyst and Windows". På iOS är `MapContextFlyout` inlindad i `#if MACCATALYST` och gör alltså ingenting. På Android är metoden tom. Mac-interaktionen skickar `previewProvider: null`. `MenuFlyout` saknar destruktiva och bockade rader och kan inte ändras under körning. | §3 |
| Samma form som `Tap.Command`? | **Ja.** En attached property som skapar ett tillståndsobjekt och kopplar på och av det när vyns handler byts. Då behövs ingen mapper per kontrolltyp och ingen registrering. | `Tap.cs:79`, `Tap.cs:113–130` |
| Hur mycket av #318 återanvänds? | **Menybyggarna rakt av**: `BuildMenu` (Apple), `Fill` (Android), `FillFlyout` (Windows), `MenuButton.Pick` och `MenuButton.Icon`. Android-byggaren sitter i dag inne i en klicklyssnare och behöver lyftas ut. | `MenuExtensions.Apple.cs:87`, `MenuExtensions.Android.cs:82`, `MenuExtensions.Windows.cs:56`, `MenuButton.cs:59`, `:81` |
| Fungerar det i list-rader? | **Ja, i `CollectionView` och `HeroCollectionView`, via radmallens rot.** Menyn byggs när den öppnas, inte när raden binds, så en rad som återanvänds kostar bara en interaktion eller lyssnare. `DataGrid` kräver en egen egenskap, eftersom dess long-press redan kopierar cellens text. | §7 |

---

## 2. Vad som redan finns i Spine

| Del | Var | Vad det betyder för kontextmenyer |
|---|---|---|
| Menymodellen | `Core/Menu/MenuElements.cs`: `MenuItems` (`:14`), `MenuAction` (`:24`), `MenuSection` (`:76`), `SubMenu` (`:104`), `MenuPicker` (`:138`) | Titel, SVG-ikon, kommando och parameter, bock, `IsEnabled`, `IsDestructive`, sektioner, undermenyer och envalsgrupper finns. Det enda som saknas för kontextmenyer är `IsVisible` (§6.5). |
| Ändringsbevakning | `MenuObserver.cs:10` bevakar hela trädet och bygger om den inbyggda menyn vid varje ändring | Behövs inte i v1: en kontextmeny kan byggas när den öppnas och är då alltid aktuell (§6.3). |
| Val av rad | `MenuButton.Pick(owner, action, picker)` (`MenuButton.cs:59`): växlar toggles, flyttar pickerns bock och kör kommandon | Återanvänds. `owner` är redan en `VisualElement`, inte en `Button`. Förslaget är en reservparameter för rader i listor (§6.3). |
| Ikoner | `MenuButton.Icon` (`MenuButton.cs:81`) renderar SVG till PNG genom `SvgBitmapLoader`. Apple gör den till template-bild (`MenuExtensions.Apple.cs:150`), Android väljer färg efter tema (`MenuExtensions.Android.cs:144`) | Samma ikoner och samma namn som i `Plugin.Maui.Spine.Svg.Icons` (`SpineIcons.Star`, `.Share`, `.Copy`, …). |
| Apple-byggaren | `BuildMenu`/`BuildChildren`/`BuildAction` (`MenuExtensions.Apple.cs:87–142`) ger en `UIMenu` | Kan returneras rakt av från en `UIContextMenuConfiguration`s `actionProvider`. |
| Android-byggaren | `MenuClickListener.OnClick` och `Fill` (`MenuExtensions.Android.cs:56–130`): `PopupMenu`, `SetForceShowIcon` (API 29+), `SetGroupDividerEnabled` (API 28+), röda destruktiva titlar | Logiken ska ut ur klicklyssnaren till en statisk `ShowPopup(anchor, items, owner, …)` som både menyknappen och kontextmenyn anropar. |
| Windows-byggaren | `FillFlyout`/`BuildItem` (`MenuExtensions.Windows.cs:56–117`) ger en `MenuFlyout` som sätts som knappens `Flyout` (`:49`) | Samma `MenuFlyout` kan sättas som `UIElement.ContextFlyout` på vilken vy som helst. |
| Registrering | `ConfigureMenus()` anropas från `ConfigureHandlers` på varje plattform (`SwitchHandlerExtensions.Apple.cs:16`, `ButtonExtensions.Android.cs:13`, `HandlerExtensions.Windows.cs:10`) | Kontextmenyn behöver ingen mapper (se nästa rad), så inget läggs till här utom möjligen `Button`-fallet på Apple (§6.6). |
| `Tap.Command` | `Tap.cs:23` (attached property), `TapState` (`Tap.cs:113`) följer `HandlerChanging`/`HandlerChanged` och kopplar plattformen med `ConnectPlatform`/`DisconnectPlatform` | Mönstret för "valfri vy": en `ContextMenuState` byggd likadant. `TapState.CornerRadius()` (`Tap.cs:233`) läser en `Border`s rundning, vilket är exakt vad förhandsvisningens `VisiblePath` behöver. |
| Header-menyer | `PageActionView` skickar `PageAction.Menu` till sina knappar (`PageActionView.cs:633–635`) | En sidas "more"-meny och radens kontextmeny kan vara samma `MenuItems`, och det är det #317 menar med "one menu model serves both". |
| `DataGrid` | Long-press på en textcell kopierar texten (`DataGrid.Press.cs:111–126`, `LongPressDuration` 500 ms i `DataGridStyleOptions.cs:53`). Rader har en `PointerGestureRecognizer` (`DataGrid.Press.cs:62`) och vid behov en `SwipeView` (`DataGrid.Swipe.cs:23`) | Konflikt med long-press. Se §7. |

---

## 3. Vad MAUI 10 har, och var det tar slut

`FlyoutBase.ContextFlyout` tar en `MenuFlyout` (`MenuFlyoutItem`, `MenuFlyoutSubItem`, `MenuFlyoutSeparator`, `IconImageSource`, tangentbordsgenvägar) och kan sättas på alla `Element`. Dokumentationen (docs-maui, `context-menu.md`) säger: *"A context menu can be added to any control that derives from Element, on Mac Catalyst and Windows."* Källan på `dotnet/maui` `main` bekräftar det:

| Plattform | Vad MAUI gör | Källa |
|---|---|---|
| Windows | `MapContextFlyout` sätter `UIElement.ContextFlyout` till den konverterade `MenuFlyout`. | `ViewHandler.Windows.cs` |
| Mac Catalyst | `MauiUIContextMenuInteraction` (internal) läggs på vyn. `UIContextMenuConfiguration` skapas med `previewProvider: null`, och delegaten implementerar bara `GetConfigurationForMenu`. | `ViewHandler.iOS.cs`, `MauiUIContextMenuInteraction.cs` |
| iOS | Den publika `MapContextFlyout` har attributet `SupportedOSPlatform("ios13.0")`, men kroppen är `#if MACCATALYST`. På iPhone och iPad händer alltså ingenting. | `ViewHandler.iOS.cs` |
| Android | `public static void MapContextFlyout(IViewHandler handler, IView view) { }`: tom. | `ViewHandler.Android.cs` |

Dokumenterade begränsningar utöver plattformarna: *"Mac Catalyst does not support displaying icons on context menu items"*, och *"It's not currently possible to add items to, or remove items from, the MenuFlyout at runtime"*. `MenuFlyoutItem` har varken destruktiv stil eller bock, och det finns ingen envalsgrupp.

Slutsatsen: MAUI:s väg är en andra menymodell som saknar det #318 redan har, och den täcker inte de två plattformar där kontextmenyer används mest. Att bygga vidare på den skulle ge Spine två menymodeller.

---

## 4. Plattformarnas byggstenar

### 4.1 iOS och iPadOS: `UIContextMenuInteraction`

Kontrollerat mot Apples dokumentation och mot bindningarna i `Microsoft.iOS.Ref.net10.0_26.2` 26.2.10217, samma SDK-linje som repot bygger med:

| API | Tillgänglig | I Microsoft.iOS |
|---|---|---|
| `UIContextMenuInteraction(delegate)`, `view.AddInteraction` | iOS 13 | `new UIContextMenuInteraction(IUIContextMenuInteractionDelegate)` |
| `UIContextMenuConfiguration(identifier:previewProvider:actionProvider:)` | iOS 13 | `UIContextMenuConfiguration.Create(INSCopying, UIContextMenuContentPreviewProvider, UIContextMenuActionProvider)` |
| Lyft-förhandsvisning: `configuration:highlightPreviewForItemWithIdentifier:` | iOS 16 (den äldre `previewForHighlightingMenuWithConfiguration:` avråds från 16) | `GetHighlightPreview` (ios16.0); `GetPreviewForHighlightingMenu` (ObsoletedOSPlatform ios16.0) |
| Tryck på förhandsvisningen: `willPerformPreviewActionForMenuWith:animator:` | iOS 13 | `WillPerformPreviewAction` |
| Formen på lyftet: `UIPreviewParameters.VisiblePath`, `.BackgroundColor`, `.ShadowPath` | iOS 13 | Ja |
| Uppdatera en öppen meny: `UpdateVisibleMenu` | iOS 14 | Ja |
| `menuAppearance`: `.rich` ("A modal menu with an optional preview") / `.compact` ("A nonmodal, compact menu with no preview") | iOS 14 | `MenuAppearance` |
| `UIButton.menu` med `showsMenuAsPrimaryAction = false` | iOS 14 | Menyn blir knappens kontextmeny (long-press). Enligt Apples dokumentation slår `menu` på och av knappens `contextMenuInteraction` automatiskt. |

Spines minimum är iOS 15 (`docs/wiki/packages.md`), så den nya lyft-metoden (16) behöver en reserv till den gamla på iOS 15, eller så får iOS 15 standardlyftet utan rundade hörn. Det senare är enklare och räcker.

Tre sätt att visa förhandsvisningen, som alla ryms i samma delegat:

1. **Inget angivet**: systemet lyfter vyn som den är, med rektangulära hörn.
2. **Targeted preview av vyn själv** med `VisiblePath` = vyns rundade form och `BackgroundColor` = vyns bakgrund. Det är vad Mail och Notes gör med list-rader, och vad HIG ber om: *"adjust the preview's clipping path to match the shape of the preview image so that its contours, such as the rounded corners, don't appear to change during animation."*
3. **Egen vy-kontroller** från `previewProvider`: en annan och större vy än den som trycktes, till exempel en tävlings detaljkort. Ett tryck på den kör `WillPerformPreviewAction`, som brukar navigera till det som förhandsvisades.

### 4.2 Mac Catalyst

Samma `UIContextMenuInteraction`, öppnad med högerklick eller Ctrl-klick. I Mac-idiomet blir menyn `compact`: ingen förhandsvisning, inget lyft. Ikoner visas i en `UIMenu` på Catalyst (MAUI:s begränsning "no icons" gäller deras `MenuFlyout`-konvertering, inte UIKit), men det är **inte verifierat** i Spines byggare. MAUI lägger sin egen `MauiUIContextMenuInteraction` på vyn bara när appen satt `FlyoutBase.ContextFlyout`. En app som sätter båda får två interaktioner, vilket ska dokumenteras som "gör inte så".

### 4.3 Android

Android har ingen systemkontextmeny med förhandsvisning, och den klassiska kontextmenyn visar inga ikoner. Enligt Android-dokumentationen (*Menus*): *"Context menu do not support item shortcuts and item icons."* Byggstenarna, kontrollerade mot `Mono.Android.dll` 36.1.43:

| API | Minsta API-nivå | Användning |
|---|---|---|
| `View.SetOnLongClickListener` | 1 | Long-press på pekskärm. Returnerar lyssnaren `true` körs ingen klick, så `Tap.Command` på samma vy körs inte efteråt. |
| `View.SetOnContextClickListener` | 23 | Högerklick med mus eller pennknapp (Chromebook, DeX, surfplatta med mus). |
| `PopupMenu(context, anchor)` + `SetForceShowIcon` | 29 för ikoner | Samma meny som #318:s menyknapp. Ankras under vyn om det finns plats, annars ovanför. |
| `PopupMenu.Gravity` | 23 | Justering mot ankaret (start/slut). |
| `View.ShowContextMenu(x, y)` | 24 | Den klassiska flytande kontextmenyn vid tryckpunkten. **Inga ikoner**, och den kräver `OnCreateContextMenu`. |
| `IMenu.SetGroupDividerEnabled` | 28 | Avdelare mellan sektioner, som i dag. |

`PopupMenu` är den väg som ger ikoner, röda destruktiva rader och samma utseende som menyknappen. Dokumentationen beskriver den som menyn för *"actions that relate to specific content"* och skiljer den från kontextmenyn *"for actions that affect selected content"*. Skillnaden är mest terminologi för Spines fall: raden är innehållet. Det som skiljer i praktiken är placeringen. En `PopupMenu` ankrad på ett helt kort visas under kortet, inte vid fingret. Ett känt knep är ett osynligt 1×1-ankare vid tryckpunkten, men det är **inte provat** här och ska bedömas på enhet innan det väljs.

Material 3 har ingen egen kontextmeny i Views-biblioteket att luta sig mot. Material Components har menyer (`ListPopupWindow`, exponerade dropdown-menyer), men inget med lyft eller förhandsvisning. Den tredjepartsvariant som gör "iOS-lika" kontextmenyer på Android (The49.Maui.ContextMenu) ritar förhandsvisningen själv. Det är samma val som Spine har gjort bort för flikar och sheets till förmån för det inbyggda.

### 4.4 Windows

`UIElement.ContextFlyout` visar flyouten vid högerklick eller *"an equivalent action, such as pressing and holding with your finger"*, och markerar `ContextRequested` som hanterad. Spines `FillFlyout` ger redan rätt `MenuFlyout` (radioknappar för pickers, toggles, undermenyer, avdelare). Menyn kan byggas när den öppnas genom `MenuFlyout.Opening`. Ingen förhandsvisning finns på Windows.

---

## 5. Alternativen

| | A. Spine: `ContextMenu.Items` (`MenuItems`) | B. Bygg på MAUI:s `FlyoutBase.ContextFlyout` | C. Tredjepartspaket | D. Listnivå (`UICollectionViewDelegate`, `registerForContextMenu(RecyclerView)`) |
|---|---|---|---|---|
| Modell | Den som finns (#318), delad med header-menyer | En andra modell (`MenuFlyout`) utan destruktiv, bock eller picker | Paketets egen (`the49:Menu`/`Action`) | A:s modell |
| iOS | Egen `UIContextMenuInteraction` | Kräver en egen iOS-mappning ändå: MAUI:s interaktion är internal och avstängd | Ja | Bästa förhandsvisningen för celler (iOS 16-API:et `GetContextMenuConfiguration(collectionView, indexPaths, point)`) |
| Android | `PopupMenu`, som menyknappen | Allt måste byggas; MAUI gör inget | Egenritad förhandsvisning | Klassisk meny utan ikoner |
| Windows | `ContextFlyout` | Fungerar i dag | Stöds inte (The49) | — |
| Förhandsvisning | iOS: lyft i v1, egen mall i steg 2 | Ingen (`previewProvider: null`) | iOS och Android | iOS |
| Risk | Låg: byggarna finns och är verifierade på simulator och emulator | Hög: att ta över en MAUI-mappning som MAUI kan ändra | Beroende: The49 finns bara som `1.0.0-alpha1` för net7 (avlistad). `DSoft.Maui.ContextMenu` 1.1.2606.161 (net10, 2026-06-16) har samma beskrivning; dess ursprung är inte kontrollerat | Hög: kräver att MAUI:s interna delegator för `CollectionView` ersätts |

**A.** D är intressant för förhandsvisningen i listor, men den förutsätter att Spine tar över `UICollectionView`-delegaten som MAUI äger. Per-vy-interaktionen på radmallens rot ger samma meny och ett lyft av raden. Det räcker tills någon visar på en skillnad som spelar roll.

---

## 6. Föreslagen design

### 6.1 API

```csharp
namespace Plugin.Maui.Spine.Extensions;

/// <summary>
/// Gives any view the platform's own context menu: a long press on touch, a right click on desktop.
/// iOS and iPadOS lift the view (or show <see cref="PreviewProperty"/>); Mac Catalyst, Android and Windows show the menu only.
/// </summary>
public static class ContextMenu
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.CreateAttached("Items", typeof(MenuItems), typeof(ContextMenu), null,
            propertyChanged: OnItemsChanged);          // creates/disposes ContextMenuState, like Tap

    /// <summary>Passed to an action's command when the action has no CommandParameter of its own.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.CreateAttached("CommandParameter", typeof(object), typeof(ContextMenu), null);

    // Step 2, iOS/iPadOS only:
    public static readonly BindableProperty PreviewProperty =           // DataTemplate
        BindableProperty.CreateAttached("Preview", typeof(DataTemplate), typeof(ContextMenu), null);
    public static readonly BindableProperty PreviewCommandProperty =    // a tap on the preview
        BindableProperty.CreateAttached("PreviewCommand", typeof(ICommand), typeof(ContextMenu), null);
}
```

Namnet `ContextMenu` krockar inte i XAML: `Microsoft.Maui.Controls` 10.0.50 har ingen typ med det namnet (kontrollerat i `Microsoft.Maui.Controls.dll`), till skillnad från `Menu`, som var skälet till `MenuButton` i #318.

### 6.2 Användning

```xml
<!-- a card with a menu; Tap.Command still opens it on a plain tap -->
<Border Tap.Command="{Binding OpenCommand}" Semantic.Merge="True"
        ContextMenu.Items="{Binding CardMenu}">
    ...
</Border>

<!-- a row in a list: one shared menu, the row's item as the parameter -->
<CollectionView ItemsSource="{Binding Competitions}">
    <CollectionView.ItemTemplate>
        <DataTemplate x:DataType="m:Competition">
            <Grid ContextMenu.Items="{Binding RowMenu, Source={RelativeSource AncestorType={x:Type vm:CompetitionsViewModel}}}"
                  ContextMenu.CommandParameter="{Binding .}">
                ...
            </Grid>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

```csharp
public MenuItems RowMenu { get; } =
[
    new MenuSection
    {
        new MenuAction("Följ", SpineIcons.Star, FollowCommand),
        new MenuAction("Lägg till i kalendern", SpineIcons.Calendar, AddToCalendarCommand),
        new MenuAction("Dela", SpineIcons.Share, ShareCommand),
    },
    new MenuAction("Ta bort", SpineIcons.Delete, RemoveCommand) { IsDestructive = true },
];

[RelayCommand] private Task Follow(Competition competition) { ... }
```

Samma `RowMenu` kan ligga på detaljsidans header med `new PageAction(null, RowMenu) { Svg = "more.svg" }`. Då får raden och sidan samma åtgärder, vilket HIG också ber om: *"Always make context menu items available in the main interface, too."*

### 6.3 Byggd när den öppnas, och parametern

Menyknappen i #318 bygger sin inbyggda meny direkt och bygger om den vid varje ändring genom `MenuObserver`. För kontextmenyn föreslås det omvända: **menyn byggs varje gång den öppnas.** Alla tre plattformarna ger en krok vid öppning: `actionProvider` körs vid long-press på Apple, Android bygger `PopupMenu` i long-click-lyssnaren och Windows har `MenuFlyout.Opening`. Två följder:

- En rad i en lista kostar en interaktion eller en lyssnare, inte en `MenuObserver` och ett menyträd per rad.
- Radens tillstånd läses vid öppning. En rad som har bytt objekt (återanvänd cell) visar rätt meny.

`ContextMenu.CommandParameter` gör en delad meny användbar för många rader. Regeln i `Pick` blir `action.CommandParameter ?? ContextMenu.GetCommandParameter(owner)`. En picker behåller sin regel (radens parameter eller raden själv). Det är en liten ändring i `MenuButton.Pick` (`MenuButton.cs:73`), en reservparameter som menyknappen skickar som `null`.

Rader vars text beror på objektet ("Följ"/"Sluta följa") hanteras på något av två sätt: objektet exponerar en egen `MenuItems` (billigt, eftersom inget byggs innan menyn öppnas), eller två rader där bara den ena är synlig (§6.5). Ett `ContextMenu.Opening`-kommando som låter vy-modellen ändra en delad meny precis före öppning är möjligt på alla tre plattformar, men läggs inte till innan någon app behöver det.

### 6.4 Förhandsvisning (iOS/iPadOS)

**Steg 1 (v1): lyft vyn med rätt form.** `GetHighlightPreview` (iOS 16+) returnerar `new UITargetedPreview(view, parameters)` där `parameters.VisiblePath` är en rundad rektangel med `Border`ns hörnradie. Samma beräkning som `TapState.CornerRadius()` gör, så den ska delas. `BackgroundColor` sätts till vyns bakgrund, annars `SystemBackground`, eftersom en radmall ofta är genomskinlig och lyftet då ser tomt ut i kanterna. På iOS 15 används standardlyftet. `GetDismissalPreview` returnerar samma, så att vyn landar där den kom ifrån.

**Steg 2: `ContextMenu.Preview` + `PreviewCommand`.** `previewProvider` skapar en `UIViewController` vars vy är mallens MAUI-vy konverterad med `ToPlatform(mauiContext)`, mätt med `Measure` och storleksatt med `PreferredContentSize`. Mallen får vyns `BindingContext`. `WillPerformPreviewAction` kör `PreviewCommand` i animatorns completion. Det är så "tryck på förhandsvisningen för att öppna" fungerar i Mail och Foton. Det här steget har den största tekniska osäkerheten: en MAUI-vy vars handler lever utanför sidans träd, och som ska kopplas från när menyn stängs (`WillEnd`). Därför är det ett eget steg.

Android och Windows ignorerar båda egenskaperna. Det ska stå i XML-dokumentationen, inte bara i wikin.

### 6.5 Ändring i den delade modellen: `MenuAction.IsVisible`

HIG: *"Hide unavailable menu items, don't dim them. Unlike a regular menu, … a context menu displays only the actions that are relevant."* Modellen har bara `IsEnabled` (`MenuElements.cs:59–61`). Förslaget är `IsVisible` (standard `true`) på `MenuAction` och `SubMenu`, och att alla tre byggarna hoppar över osynliga rader. Det gäller då även menyknappar. Det behövs för "Följ/Sluta följa" med en delad meny, och det är den enda ändringen i #318:s publika yta.

### 6.6 Samspel med det som redan finns

| Kombination | Beteende |
|---|---|
| `Tap.Command` + `ContextMenu.Items` | Det vanliga fallet: tryck öppnar, long-press visar menyn. **Apple:** `PressRecognizer` känner igen samtidigt med allt (`Tap.Apple.cs:174`) och tänder sin markering efter 70 ms (`:16`). Markeringen är ett `CALayer` i vyn (`:131`) och kommer alltså med i lyftet om den inte släcks. `ContextMenuState` ska anropa `TapState.CancelPress()` när menyn visas (`WillDisplayMenu`). Om interaktionen avbryter pekningen så att kommandot inte körs vid släpp är **inte verifierat**. **Android:** long-click som returnerar `true` förhindrar klicket; rippeln (`Tap.Android.cs:31`) syns under hållningen, som i systemappar. **Windows:** högerklick ger ingen `Tapped`; beteendet för press-and-hold är **inte verifierat**. |
| `MenuButton.Items` + `ContextMenu.Items` på en `Button` | Apple: båda använder `UIButton.Menu`. `ShowsMenuAsPrimaryAction = true` betyder tryck, `false` betyder long-press. En knapp kan inte ha båda, och **menyknappen vinner**. En `Button` med bara `ContextMenu.Items` får sin meny som `button.Menu` med `ShowsMenuAsPrimaryAction = false`, inte en extra interaktion, eftersom `UIControl` har en egen `contextMenuInteraction`. Android och Windows har ingen konflikt (click- mot long-click-lyssnare, `Flyout` mot `ContextFlyout`). |
| `FlyoutBase.ContextFlyout` + `ContextMenu.Items` | Windows och Catalyst: två mekanismer på samma vy. Dokumenteras som "välj en". Spine sätter `ContextFlyout` på Windows och skriver över MAUI:s om båda finns. |
| Tillgänglighet | Apple: huruvida VoiceOver erbjuder menyn automatiskt för en vy med `UIContextMenuInteraction` är **inte kontrollerat**. Android: long-click-åtgärden får en etikett genom `ViewCompat.ReplaceAccessibilityAction(ActionLongClick, "Åtgärder")` från `SpineStrings`, så att TalkBack säger vad en long-press gör. Att även lägga ut varje menyrad som en egen tillgänglighetsåtgärd är möjligt och värt att pröva. |

---

## 7. Listor: `CollectionView`, `HeroCollectionView` och `DataGrid`

**`CollectionView` och `HeroCollectionView`.** `HeroCollectionView` är en `CollectionView` (`HeroCollectionView.cs:22`) och behöver inget eget. `ContextMenu.Items` sätts på radmallens rot. Apple lägger interaktionen på MAUI-vyn i cellens `ContentView`, Android på radens vy i `RecyclerView`, Windows på elementet i listobjektet. Återanvändning av celler är ofarlig eftersom menyn byggs vid öppning (§6.3) och parametern läses från den bindning som gäller just då. Att scrolla avbryter long-press på alla tre plattformarna, eftersom det är systemets egen gest. Kvar att kontrollera på enhet: att `CollectionView`s urvalsmarkering (`SelectionMode`) och lyftet inte krockar på iOS, och att long-click når radvyn på Android när radmallen har MAUI-gestigenkännare. MAUI:s gesthantering på Android sätter egna touch-lyssnare. Det är **oprövat** om de konsumerar long-click.

**`DataGrid`.** Rader byggs i kod, inte från en mall, och har redan en long-press: en textcell kopieras (`DataGrid.Press.cs:111–126`). Att lägga `ContextMenu.Items` på raden utifrån går inte, och om det gick skulle två long-press slåss om samma finger. Förslaget är en egenskap på gridden:

```xml
<spine:DataGrid ItemsSource="{Binding Games}" RowContextMenu="{Binding GameMenu}" />
```

- Med `RowContextMenu` satt öppnar long-press menyn med radens objekt som parameter, och kopieringen blir en rad i menyn ("Kopiera *Kolumnnamn*" överst), eftersom gridden redan vet vilken cell fingret träffade (`FindCell`, `DataGrid.Press.cs:82`). Utan `RowContextMenu` är allt som i dag.
- På Apple ger gridden raden en `UIContextMenuInteraction` och stänger av sin egen long-press-timer för den raden. På Android och Windows kan gridden öppna menyn själv från `OnRowLongPress`, genom samma `ShowPopup`/`ContextFlyout.ShowAt` som `ContextMenuState` använder.
- Svepåtgärder (`LeftSwipeActions`/`RightSwipeActions`) är en annan gest och finns kvar. De har en egen modell (`DataGridSwipeAction`, `DataGridSwipeAction.cs:15`) med färger och sökvägsbindningar. Att låta dem bli `MenuAction` ingår inte här.

Hur `DataGrid`s egen tryckhantering beter sig tillsammans med en `UIContextMenuInteraction` på samma rad är det mest osäkra i hela förslaget. Därför ligger det i ett eget steg (§10).

---

## 8. Plattformar

| Plattform | v1 | Förhandsvisning | Anmärkning |
|---|---|---|---|
| iOS 16+, iPadOS 16+ | `UIContextMenuInteraction`, `UIMenu` från #318:s byggare | Lyft av vyn med rundad `VisiblePath`; egen mall i steg 2 | iPad med mus eller styrplatta: högerklick ger compact-menyn |
| iOS 15 | Samma | Standardlyft (rektangulärt) | Den gamla lyft-metoden kan läggas till om någon saknar det |
| Mac Catalyst | Samma interaktion, högerklick | Nej (compact) | Ikoner i menyn ej verifierade |
| Android 5–9 (API 21–28) | `PopupMenu` vid long-click; högerklick från API 23 | Nej | Ikoner visas från API 29 (`SetForceShowIcon`), avdelare från 28 |
| Android 10+ | Samma, med ikoner | Nej | Menyn ankras vid vyn, inte vid fingret |
| Windows | `ContextFlyout` med #318:s `MenuFlyout` | Nej | Högerklick och press-and-hold ger systemets eget beteende |

---

## 9. Vad som inte går, och vad som inte är verifierat

Studien är gjord i en Linux-container utan Mac, simulator, emulator eller enhet. **Inget i den är provat på en plattform.** API-tillgänglighet är kontrollerad mot Apples dokumentation, `Microsoft.iOS`-referensen 26.2.10217, `Mono.Android` 36.1.43 och MAUI:s källa på `main`. Beteende är det inte.

1. **Ingen förhandsvisning på Android, Windows eller Mac.** Plattformarna har ingen. Att rita en själv (som The49) går emot linjen med inbyggda kontroller och avråds.
2. **Tap-markeringen i lyftet** (§6.6): resonemang utifrån koden, inte observerat. Detsamma gäller om `Tap.Command` körs när fingret släpps efter att menyn öppnats.
3. **Long-click på Android under MAUI:s gestigenkännare** (§7): oprövat. Om MAUI:s touch-lyssnare konsumerar händelserna behöver `ContextMenuState` en egen `GestureDetector`, vilket är mer kod än planerat.
4. **`PopupMenu`s placering** på stora vyer, och knepet med ankare vid tryckpunkten: oprövat.
5. **Sektionstitlar i kontextmenyer på Apple.** #318 konstaterade att UIKit inte ritar inline-sektioners titel i en *knappmeny*. Om det är likadant i en kontextmeny är inte kontrollerat.
6. **Egen förhandsvisningsmall** (§6.4 steg 2): en MAUI-vy med handler utanför sidans träd, i en vy-kontroller som UIKit äger. Mätning, temabyte och frånkoppling vid `WillEnd` är oprövade.
7. **VoiceOver och menyn** (§6.6): inte kontrollerat om den erbjuds automatiskt.
8. **Ikonrendering vid öppning.** `MenuButton.Icon` renderar SVG synkront (`MenuButton.cs:93`). Vid long-press sker det i `actionProvider`, alltså på väg in i animationen. Med en handfull ikoner bör det gå fort, men det är inte mätt. En liten cache per `(svg, storlek, färg)` är en rad kod om det behövs.
9. **En iakttagelse i passing:** Windows-byggaren renderar ikoner svarta oavsett tema (`MenuExtensions.Windows.cs:121`), medan Android väljer färg efter tema (`MenuExtensions.Android.cs:144`). Om `ImageIcon` visar bitmappen som den är blir ikonerna svåra att se i mörkt tema, både i dagens menyknappar och i kontextmenyn. Inte kontrollerat på Windows.

---

## 10. Leveransplan

1. **Dela ut byggarna.** Android: flytta `Fill`/`AddAction`/`Icon` ur `MenuClickListener` till en statisk `ShowPopup(anchor, items, owner, parameter)`. `MenuButton.Pick` får reservparametern. `MenuAction.IsVisible`/`SubMenu.IsVisible` läggs till och respekteras av alla tre byggarna. Menyknapparna ska bete sig exakt som förut. Det verifieras i `MenusPage`.
2. **`ContextMenu.Items` + `CommandParameter`.** `ContextMenu` och `ContextMenuState` (mönstret från `TapState`). Apple: interaktion, `GetConfigurationForMenu` med `BuildMenu` vid öppning, lyft med `VisiblePath` på iOS 16+, `Button`-fallet via `UIButton.Menu`, och att Tap-markeringen släcks. Android: long-click, context-click (API 23+) och tillgänglighetsetikett. Windows: `ContextFlyout` byggd i `Opening`.
3. **Sample och wiki.** En sida `Pages/ContextMenus` i `MauiSpineSampleApp`: ett kort med `Tap.Command` och meny, en `CollectionView` med delad radmeny och parameter, samma meny på header-baren. Därefter ett avsnitt i `docs/wiki/menus.md` (inte en ny sida: det är samma modell) och en rad i `/spine-controls`. Verifiera på iPhone-simulatorn, Pixel-emulatorn och Mac Catalyst; Windows via CI.
4. **`DataGrid.RowContextMenu`** (§7), med kopiering som menyrad. Eget steg på grund av konflikten med dagens long-press.
5. **`ContextMenu.Preview` + `PreviewCommand`** (§6.4 steg 2), när en app visar ett konkret behov. Orienteras tävlingsrad är den troliga första.
6. **Apparna.** Orientera (Följ, Lägg till i kalendern, Dela), Puckkoll (Bevaka, Öppna i SHL-appen), Almanacka (Kopiera namn, Dela). Beviset på att en deklaration räcker för header och rad.

Beslut som behöver ägaren före steg 2: att `PageAction` inte blir radmodellen (§1); `IsVisible` i den delade modellen (§6.5); och om `DataGrid`s kopiering ska flytta in i menyn när en meny finns (§7).

---

## Beslut (2026-09-30)

Jonatan gick igenom studiens frågor 2026-09-30 och följde rekommendationerna. Rader märkta **Förslag** saknade rekommendation i studien; där står ett förslag med skäl, som gäller tills han säger annat.

- **Modellen.** `MenuItems` från #318 delas mellan header, menyknapp och kontextmeny. `PageAction` blir inte radmodellen (§1).
- **`IsVisible`.** Läggs till på `MenuAction` och `SubMenu` i den delade modellen, och gäller då även menyknappar (§6.5).
- **`DataGrid`.** När `RowContextMenu` är satt flyttar long-press-kopieringen in i menyn som en rad "Copy <kolumn>" (§7).
- **Ytan.** En egen attached property `ContextMenu.Items` (alternativ A), inte MAUI:s `FlyoutBase.ContextFlyout`, ett tredjepartspaket eller delegater på listnivå.
- **Förhandsvisning.** Vyn själv lyfts i v1 (iOS 16+, standardlyft på iOS 15). `ContextMenu.Preview` och `PreviewCommand` blir steg 2 när en app behöver dem.
- **Andra plattformar.** Ingen egenritad förhandsvisning på Android, Windows eller Mac.
- **Android.** `PopupMenu` ankrad till vyn. Om ankaret ska vara en 1×1-vy i tryckpunkten avgörs på enhet.
- **Knapp med både `MenuButton.Items` och `ContextMenu.Items`.** Menyknappen vinner.
- **`ContextMenu.Opening`.** Utelämnas tills en app behöver det.
- **Dokumentation.** I `docs/wiki/menus.md`, ingen ny wikisida.

---

## 11. Referenser

- Issue #306, *Native context menus on any view*: https://github.com/jonatansoderberg/Maui.Spine/issues/306
- Issue #317, prioriterad backlog: https://github.com/jonatansoderberg/Maui.Spine/issues/317
- Issue #318, menyknappar (implementerad): https://github.com/jonatansoderberg/Maui.Spine/issues/318, `issues/318-menu-buttons.md`, `docs/wiki/menus.md`
- Apple, `UIContextMenuInteraction`: https://developer.apple.com/documentation/uikit/uicontextmenuinteraction
- Apple, `UIContextMenuInteractionDelegate` (inklusive `configuration:highlightPreviewForItemWithIdentifier:`, iOS 16): https://developer.apple.com/documentation/uikit/uicontextmenuinteractiondelegate
- Apple, `UIContextMenuConfiguration.init(identifier:previewProvider:actionProvider:)`: https://developer.apple.com/documentation/uikit/uicontextmenuconfiguration/init(identifier:previewprovider:actionprovider:)
- Apple, `UIContextMenuInteraction.appearance` (`rich`/`compact`): https://developer.apple.com/documentation/uikit/uicontextmenuinteraction/appearance
- Apple, `UIPreviewParameters.visiblePath`: https://developer.apple.com/documentation/uikit/uipreviewparameters/visiblepath
- Apple, `UICollectionViewDelegate.collectionView(_:contextMenuConfigurationForItemsAt:point:)`: https://developer.apple.com/documentation/uikit/uicollectionviewdelegate/collectionview(_:contextmenuconfigurationforitemsat:point:)
- Apple, `UIButton.menu` och `UIControl.showsMenuAsPrimaryAction`: https://developer.apple.com/documentation/uikit/uibutton/menu, https://developer.apple.com/documentation/uikit/uicontrol/showsmenuasprimaryaction
- Apple HIG, *Context menus*: https://developer.apple.com/design/human-interface-guidelines/context-menus
- Android, *Add menus* (kontextmenyer utan ikoner, `PopupMenu`): https://developer.android.com/develop/ui/views/components/menus
- MAUI-dokumentation, *Display a context menu* (källa i docs-maui): https://github.com/dotnet/docs-maui/blob/main/docs/user-interface/context-menu.md
- MAUI-källor på `main`: `src/Core/src/Handlers/View/ViewHandler.iOS.cs`, `ViewHandler.Android.cs`, `ViewHandler.Windows.cs`, `src/Core/src/Platform/iOS/MauiUIContextMenuInteraction.cs` (https://github.com/dotnet/maui)
- Microsoft, `UIElement.ContextFlyout`: https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.uielement.contextflyout (via sökresultat; sidan kunde inte hämtas från containern)
- The49.Maui.ContextMenu: https://github.com/the49code/The49.Maui.ContextMenu, NuGet `1.0.0-alpha1` (net7)
- DSoft.Maui.ContextMenu: https://www.nuget.org/packages/DSoft.Maui.ContextMenu (1.1.2606.161, net10)
- Bindningar kontrollerade i: `Microsoft.iOS.Ref.net10.0_26.2` 26.2.10217, `Microsoft.Android.Ref.36` 36.1.43, `Microsoft.Maui.Controls.Core` 10.0.50 (NuGet)
