# Shared elements and zoom between pages in Spine (study, rev 1)

**Status:** Study, with the owner's decisions from 2026-09-30 in the section [Decisions](#decisions-2026-09-30). Nothing implemented; the spike remains. Issue: [#304](https://github.com/jonatansoderberg/Maui.Spine/issues/304) (P1 in the backlog [#317](https://github.com/jonatansoderberg/Maui.Spine/issues/317), which proposes a spike for exactly this idea because it carries the most design risk). The study was done in a Linux container without a Mac, Windows or a device: Spine's code, the platform documentation and the bindings have been read, nothing has been run.
**Question:** Can Spine provide shared elements (an image or a card that flies from the list to the detail page) and zoom (the page grows out of the tapped card and shrinks back with the back swipe) between pages — with an attribute on the source and destination view, without animation code in the app — and can the platforms' own APIs (iOS 18 `.zoom`, Android's shared elements and container transform, Windows `ConnectedAnimationService`) do the job?
**Answer:** Yes, but for region pages Spine has to draw the transition itself. Spine's stack is virtual: all pages in a region are MAUI views in the same `ContentPage`, in two `ContentView` hosts, so there is no `UIViewController`, no `Fragment` and no `Frame` navigation per page to hang `.zoom` or fragment transitions on. The proposal is an attached property `Hero.Tag`, an overlay layer in `NavigationRegion` that flies a snapshot of the source to the destination (and zooms the whole page when the tag sits on the destination page's root), the same choreography on every platform, driven by the back swipe that already exists — and on Android by the progress of the predictive back gesture. Native is used where Spine already has a native presentation: iOS 18+ `.zoom` on the sheet's `UIViewController`, and on the full-screen presentation that the lightbox ([#311](https://github.com/jonatansoderberg/Maui.Spine/issues/311)) needs. Windows can use `ConnectedAnimationService` as the engine for the flight, because it works at view level.

---

## 1. The conclusion in short

| Question | Answer | Evidence |
|---|---|---|
| Can iOS 18 `.zoom` be used for Spine's push? | **No.** `preferredTransition` takes effect on a push in a `UINavigationController` and on modal presentation. A region page is not a view controller; it is swapped into `NavigationRegion`'s front host. | Apple, *Enhancing your app with fluid transitions*; WWDC24 10145; `NavigationRegionViewModel.cs:203-243`, §2, §3.1 |
| Can `.zoom` be used anywhere in Spine? | **Yes, for sheets.** Spine's sheet on iOS is a real `UIViewController` that is presented modally (`PresentViewController`), and Apple says the same API works for sheet and full-screen presentations. It is also the route for the lightbox. | `BottomSheetPageExtensions.Apple.cs:90-139`; WWDC24 10145, §8 |
| Android: fragment transitions? | **No**, Spine has no fragment per page. `MaterialContainerTransform` does work between two views in the same activity, but it cannot be driven by a gesture (`isSeekingSupported` is missing, default `false`) and so cannot follow predictive back. | Material *Motion.md* ("Transition between Views"); `MaterialContainerTransform.java`; AndroidX `Transition.isSeekingSupported`, §3.2 |
| Windows: `ConnectedAnimationService`? | **Yes, as the engine.** It takes two arbitrary `UIElement`s (`PrepareToAnimate` on the source, `TryStart` on the destination) and needs no `Frame`; WinUI 3 Gallery uses it within one page. There is no gesture to drive it with, but Windows has no back swipe to follow. | windows-dev-docs *connected-animation.md*; WinUI-Gallery `CardPage.xaml.cs`, §3.3 |
| So who draws? | **Spine**, in `NavigationRegion`, which owns both hosts, the header bar and the back swipe. Not `PagePresenter` as the issue sketches: it holds one page, not two. | `NavigationRegion.cs:68-127`, §5 |
| How are the source and destination views marked? | `Hero.Tag` on both, bound to the same id. The tag on the destination page's **root** means zoom; on a view **inside** the destination page it means shared element. No new parameter on `NavigateToAsync`. | §5.1 |
| Interactive? | **Yes on iOS** through the existing edge pan (`NavigationRegion.cs:496-601`). **Android** requires Spine's back callback to also take `HandleOnBackProgressed`; AndroidX Activity 1.10, which MAUI 10.0.50 pins, has it. | §6 |
| Recycled cells? | Possible, if the lookup is done per tag at every transition and never holds on to a cell. Scrolled out of view ⇒ ordinary back transition. | §7 |
| Reduce Motion | Fades instead of flying. `ReducedMotion.IsOn` already exists; `DefaultSpineTransitions` does not read it today. | `ReducedMotion.cs`, `PageActionView.cs:104` |

---

## 2. How Spine navigates today

### 2.1 A region is two hosts and a header

`NavigationRegion` (`Presentation/NavigationRegion.cs`) is a `ContentView` with a `Grid` in four layers, in this order: `_contentHostBack`, a dimming layer for the back swipe, `_contentHostFront` and `HeaderBarView` on top (`NavigationRegion.cs:68-110`). The hosts are bound to `NavigationRegionViewModel.BackView`/`FrontView`, two `PagePresenter`s (a `Grid` with the page, the title row and an optional footer, `PagePresenter.cs:13-100`).

Push (`NavigationRegionViewModel.cs:203-243`): the new page is put in `FrontView`, the old one in `BackView`, the header bindings are updated, and then `ISpineTransitions.AnimateNavigateToShowAsync(FrontView)` and `AnimateNavigateToHideAsync(BackView)` run **immediately** (line 229) — before the new page has been laid out. After that the old page is detached from the tree (`BackView.Content = null`, line 231). Pop (`BackAsync`, lines 280-341) does the same thing in reverse and attaches the previous page to `BackView` again only when it is about to be shown.

`ISpineTransitions` (`Presentation/ISpineTransitions.cs`) sees only one `View` per call (the presenter), never which page is being left, which one is coming or where the user tapped. The default implementation slides sideways, 300 ms (250 on Windows), and is registered as a singleton (`MauiAppBuilderExtensions.cs:53`).

`NavigationService` delivers the navigation parameter **before** the page is pushed (`NavigationService.cs:100-107`): a destination view's `Hero.Tag` bound to an id that the view model sets in `OnNavigationParameterAsync` is therefore already set when the transition begins. That is a precondition for §5.

### 2.2 The back swipe

The edge pan is a MAUI `PanGestureRecognizer` on the front host (`NavigationRegion.cs:114-121`). A press within the left 25% accepts the gesture (`OnPointerPressed`, lines 402-412); on iOS and Mac Catalyst the built-in `UIPanGestureRecognizer` gets a `ShouldBegin` that only lets through a rightward drag that is more horizontal than vertical (`NavigationRegion.Apple.cs:21-40`, issue #267). During the drag the front host moves with the finger and the previous page is revealed with a clip (`ApplyBackReveal`, lines 414-445); past a third of the width the pop completes, otherwise the page springs back (lines 559-599). `StartInteractiveBack` attaches the previous page to `BackView` when the drag begins (`NavigationRegionViewModel.cs:423-455`).

On Android the system back goes to an `OnBackPressedCallback` that only implements `HandleOnBackPressed` (`SpineApplication.Android.cs:167-180`): nothing moves during the gesture, the page slides only when it is released.

### 2.3 Sheets and tabs

- **iOS/Mac Catalyst:** the sheet is `SpineSheetViewController`, presented as `PageSheet` with `ModalInPresentation = true` so that every drag-to-dismiss goes through the delegate and the page's back guard (`BottomSheetPageExtensions.Apple.cs:90-139`).
- **Android:** `BottomSheetDialog`, that is, a window of its own (`BottomSheetPageExtensions.Android.cs:114`, 486).
- **Windows:** a WinUI popup in the same window (`BottomSheetPageExtensions.Windows.cs:40-70`).
- **Tabs:** `SpineTabbedHostPage` has one `NavigationRegion` per tab; tab switches do not go through `ISpineTransitions` (docs/proposals/spine-tab-host.md).

### 2.4 What this means for #304

No platform sees Spine's page changes as page changes. That is what makes the idea possible — Spine has both pages in hand at the same time, in the same `Grid` — and it is the same thing that shuts out the platforms' navigation transitions. And two things in today's pipeline have to change for a flight to land in the right place: the destination page must have time to be laid out before the animation starts, and the previous page must be attached to the tree and laid out before a return flight can know where to land.

---

## 3. The platforms' building blocks

### 3.1 iOS and Mac Catalyst

- `UIViewController.preferredTransition` and `UIViewController.Transition.zoom(options:sourceViewProvider:)`, iOS/iPadOS/Mac Catalyst 18.0. The source view provider is called both when the page is presented and when it is dismissed; Apple recommends looking the view up by a stable id instead of holding a `UIView` or an `IndexPath`.
- Applies to a push in a `UINavigationController` and, according to WWDC24 10145, "the fullScreenCover and sheet presentation APIs in both SwiftUI and UIKit". The transition is continuously interactive: it can be grabbed and dragged midway, and an interrupted push becomes a pop.
- *"The fluid zoom transitions are available on iPhone and iPad … The API is available on other platforms, but the system uses the default transitions."* So Mac Catalyst probably does not get the zoom, even though the API exists there; the issue's "Full (to verify)" is, going by the documentation, closer to "no". Whether Catalyst in the iPad idiom counts as iPad the documentation does not say.
- `ZoomOptions`: `interactiveDismissShouldBegin`, `alignmentRectProvider`, `dimmingColor`, `dimmingVisualEffect`. iOS 26 added `zoom(options:sourceBarButtonItemProvider:)` (the sheet grows out of a button in the navigation bar).
- The binding exists in .NET: `UIViewController.PreferredTransition`, `UIViewControllerTransition.Zoom(UIZoomTransitionOptions, Func<UIZoomTransitionSourceViewProviderContext, UIView>)` and `UIZoomTransitionOptions.InteractiveDismissShouldBegin` in `dotnet/macios` `src/uikit.cs` (main). Not grepped in the repo's Microsoft.iOS 26.2 package — the container has no workloads.
- Known problem: zooming into a sheet with a medium detent gives a jerk in the dimming after the transition (Apple forum 788257, SwiftUI, unanswered).
- Outside zoom there is no view-level API for shared elements in UIKit. Custom transitions are built with `snapshotView(afterScreenUpdates:)` and `UIViewPropertyAnimator` — the same building blocks Spine would use.

### 3.2 Android

- **Fragment shared elements** require a fragment per page. Spine's pages are views in MAUI's single activity (in the tab host the tabs are in fragments, but the pages inside a tab are not). Not applicable.
- **`MaterialContainerTransform` between views:** set `startView`/`endView`, `TransitionManager.beginDelayedTransition(container, transform)`. It is documented and fits Spine's structure. Material 1.12.0.5 is pinned by MAUI 10.0.50 (`eng/AndroidX.targets`), which the repo builds with (`Directory.Packages.props:10`). But the class does not override `isSeekingSupported`, which returns `false` in AndroidX `Transition`: it cannot be scrubbed by a gesture.
- **Predictive back:** `OnBackPressedCallback.HandleOnBackStarted/HandleOnBackProgressed/HandleOnBackCancelled` with `BackEventCompat.Progress` requires AndroidX Activity 1.8; MAUI 10.0.50 pins 1.10.1.3. Scrubbable transitions (`TransitionManager.controlDelayedTransition`) require AndroidX Transition 1.5; MAUI pins 1.6.0.1. For apps with targetSdk 36 on Android 16, predictive back is on by default and `onBackPressed` is no longer called; Spine already uses the dispatcher callback and is not affected by that.
- **Images:** MAUI loads images with Glide (`src/Core/AndroidNative/maui/.../glide`). If Glide delivers hardware bitmaps they cannot be drawn in a software `Canvas` — a snapshot through `View.Draw` can then throw. Not verified; see §10.

### 3.3 Windows

- `ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(key, source)` on the source, `GetAnimation(key).TryStart(destination)` on the destination. Between the steps it "appears frozen above other UI"; if the animation is not started within three seconds it is discarded, and Microsoft advises not waiting more than ~250 ms. The configurations `Gravity` (forward, default) and `Direct` (back, 150 ms), and "coordinated elements" that travel along with the flight.
- The documentation says that it "can be applied to any experience where you are changing content in a UI"; WinUI 3 Gallery runs it within one page (`CardPage.xaml.cs`). Microsoft's list of WinRT APIs that are not supported in desktop apps mentions `ConnectedAnimationService` — but it links the `Windows.UI.Xaml` variant; MAUI uses `Microsoft.UI.Xaml`, whose documentation and Gallery show that it works. Untried in Spine.

---

## 4. The alternatives

| | A. The platforms' navigation | B. View-level APIs per platform | C. Spine draws (recommended for regions) | D. iOS `.zoom` on presentations (recommended for sheets/lightbox) |
|---|---|---|---|---|
| What | `UINavigationController`, fragment per page, `Frame.Navigate` | iOS: nothing; Android: `MaterialContainerTransform` view→view; Windows: `ConnectedAnimation` | Snapshot + overlay + choreography in `NavigationRegion` | `sheetVc.PreferredTransition = Zoom(...)` |
| Requires | That Spine changes its navigation model | Three implementations | A new layer in the region, some platform code for snapshot and coordinates | That the page is already presented as a view controller |
| Interactive back | Free | No on Android (not scrubbable), no gesture on Windows, missing on iOS | Yes: the edge pan on iOS, predictive back on Android | Free (drag down, edge) |
| Same look everywhere | No | No | Yes | — (iOS/iPadOS only) |
| Risk | Rewrite of Spine's core | Three behaviors to keep in sync | Frame rate while the destination page loads; layout timing | Interplay with `ModalInPresentation` and detents |

**A** is ruled out: Spine exists to own the navigation, and every tab, sheet and back swipe builds on the virtual stack. **B** gives three different behaviors and no interactive way back on Android — the thing that makes the zoom worth having. **C** is the only one that works for region push on every platform and that can follow the finger. **D** costs almost nothing and gives the genuine iOS zoom where it is available; it is also exactly what the lightbox needs.

Windows is a borderline case: `ConnectedAnimation` does internally what C does (freezes the source above the UI and flies it), with Fluent's own curve, and Windows has no gesture that needs to scrub. The proposal is to let Windows use it behind the same layer if the spike shows that it starts in time (§10); otherwise C.

---

## 5. Proposed design

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
<!-- the list page: one cell -->
<Border Hero.Tag="{Binding Id, StringFormat='competition-{0}'}">
    <Image Source="{Binding Poster}" Hero.Tag="{Binding Id, StringFormat='poster-{0}'}" />
</Border>

<!-- the detail page, shared element: the image in the header receives the poster -->
<Image Source="{Binding Poster}" Hero.Tag="{Binding Id, StringFormat='poster-{0}'}" />

<!-- the detail page, zoom: the tag on the page's root, so the whole page grows out of the card -->
<ContentView x:Class="Orientera.Pages.CompetitionPage"
             Hero.Tag="{Binding Id, StringFormat='competition-{0}'}"> ... </ContentView>
```

The call is unchanged: `await _navigation.NavigateToAsync<CompetitionPage, CompetitionId>(id);`.

- **The tag's position decides the mode.** Root ⇒ zoom (Material's "container transform": the card becomes the page). View inside the page ⇒ shared element, with the rest of the page fading in instead of sliding in. No matching tag ⇒ today's transition.
- **No parameter on `NavigateToAsync`.** The issue's sketch `NavigateAsync<…>(id, transition: SpineTransition.Zoom)` would have to be added to six methods in `INavigationService` (`NavigateToAsync`, `ShowAsync` and `NavigateToWithResultAsync`, two overloads each), and it still cannot know *which* view the user tapped — only the tags know that. If the owner wants to be able to turn the transition off per call, a `SpineOptions.Transitions.SharedElements` switch and `Hero.Tag = null` are simpler.
- **The name.** "Hero" is already used for `HeroCollectionView` (the collapsing image header). It is no conflict in code, and that header is the most common *destination* for a shared image, but it is the owner's choice whether the name should be `Hero.Tag` (the issue, Flutter's term) or something else.

### 5.2 The sequence on push

1. Before the new page is attached: `HeroRegistry` collects visible views with a tag on the current page (only those that have a handler, are `IsVisible`, and lie within the page's visible area).
2. The new page is attached as today, but with `Opacity = 0`, and the region waits for its first layout (`SizeChanged` on the presenter) — for a time window at most, e.g. 100 ms; after that, today's transition.
3. Match tags between the two pages. No match ⇒ today's transition.
4. Measure both views' rectangles in the region's coordinates. MAUI's `GetBoundingBox` is `internal`, so Spine needs its own conversion per platform (`ConvertRectToView`, `GetLocationInWindow`, `TransformToVisual` — all already used in the repo, e.g. `MaterialExtensions.Android.cs:197`).
5. Take a snapshot of the source at platform level and put it in an overlay layer between the front host and the header bar (an empty, `InputTransparent` MAUI layer whose platform view receives the images; the sheet's blur overlay places a native view over the page in the same way, `BottomSheetPageExtensions.Apple.cs:244-261`). Hide the source and the destination (`Opacity = 0`).
6. Animate: a shared element flies rectangle and corner radius from source to destination while the destination page fades in; zoom scales and clips the whole destination page from the source's rectangle while the previous page is dimmed.
7. Land: show the destination, remove the snapshot, restore the source's `Opacity` through the view reference, not through the tag (§7).

Pop does the same thing in the other direction. The previous page is attached to `BackView` before the animation (that is already done, `NavigationRegionViewModel.cs:315`), the tags are looked up again and the region waits for layout before it knows where the flight lands.

### 5.3 Images, aspect ratio and corners

A snapshot of a square thumbnail in `AspectFill` lacks the parts that were cropped away and cannot become a wide header image without cross-fading to the destination's image. Two cases:

- **`Image` with a tag:** the overlay draws the same `ImageSource` (already in the cache) with `AspectFill` and animates the frame — the crop opens up as in the Photos app. It is the most common case in the issue's examples and the best-looking one.
- **Everything else (cards, `Border`, text):** two snapshots, source and destination, that cross-fade into each other while the frame flies. The corner radius from the source's `StrokeShape` is interpolated toward 0 (or the destination's).

Guideline for apps: pass the thumbnail's URL along in the navigation parameter, so that the destination page's image has its source when the transition begins and does not wait for `TaskState`/`StateView` (#302). A tag that sits inside a `StateView` that is still showing the loading state does not exist, and then it is today's transition.

### 5.4 `ISpineTransitions`

The flight is run by the region, not by `ISpineTransitions`, but the pages' part of a hero transition (fade-in, dimming) and the durations need a place. Two new members with a default implementation (the same technique `ResetHiddenViewAsync` already uses), so that apps with their own transitions keep compiling and get Spine's hero behavior:

```csharp
Task AnimateSharedAsync(SharedTransitionContext context) => SpineHeroTransitions.AnimateSharedAsync(context);
Task AnimateZoomAsync(ZoomTransitionContext context) => SpineHeroTransitions.AnimateZoomAsync(context);
```

`context` carries the direction, both presenters, the rectangles and a `Progress` value for the interactive path (§6).

---

## 6. Interactivity and back swipe

- **iOS/Mac Catalyst.** The edge pan exists and is already limited to a rightward drag from the edge (#267). When the page came in with zoom, the reveal clip (`ApplyBackReveal`) is replaced by the zoom's geometry: `progress = deltaX / width`, the page shrinks toward the source's rectangle and follows the finger, the previous page brightens. Release past the threshold (today 0.33, `NavigationRegion.cs:41`) ⇒ complete from the current position; below ⇒ back to full screen. Shared element during the drag: the flight is scrubbed with the same `progress`.
- **Drag down to dismiss** (like iOS 18's zoom) is not part of v1: it collides with vertical scrolling and must begin only when the page's list is at the top. It is the same trade-off that `interactiveDismissShouldBegin` exists for in UIKit.
- **Android.** `SpineRegionBackCallback` gets `HandleOnBackStarted` (attaches the previous page, like `StartInteractiveBack`), `HandleOnBackProgressed` (`BackEventCompat.Progress` scrubs the flight), `HandleOnBackCancelled` (back) and `HandleOnBackPressed` (complete). That also gives a predictive back gesture for *ordinary* pops — a larger change of behavior than #304 and worth an issue of its own.
- **Windows.** No gesture; the flight is non-interactive.
- **The back guard.** `OnBackRequestedAsync` is asked today only when the drag is completed (`NavigationRegionViewModel.cs:457-470`); if it says no, the page springs back from where it is. For zoom that means a page that jumps up from the card's size. The spike is to show what that looks like; one alternative is to ask the back guard when the drag *begins*.
- **Interrupted push.** Apple turns an interrupted zoom push into a pop. Spine only needs to prevent a new navigation from starting in the middle of a flight — or finish the flight immediately — because `NavigateToAsync` does not protect itself against overlapping calls today.

---

## 7. CollectionView cells that are recycled

- **The registry never holds a cell.** `HeroRegistry` holds weak references and is updated when `Hero.Tag` changes — which happens every time a cell is rebound to another row. At lookup it checks that the view's tag is *still* the one being sought, that it has a handler and that it is visible.
- **The snapshot is taken before the navigation.** Once the list is detached (`BackView.Content = null`) there is nothing to photograph.
- **Restore by view reference.** The source is hidden during the flight; the restore is done on the view that was hidden, in a `finally`, so that a cell that has been recycled in the meantime is never left invisible.
- **On the way back** the row may have been scrolled away, the list may have been reloaded, or the detail page may have been paged to another item (its tag changed). The lookup is then done on what is in the destination page's tag *now*; if there is no visible cell with that tag, it is today's back transition. Scrolling the list to the row first (as Windows `TryStartConnectedAnimationAsync` does) is a later improvement.
- **Detached and re-attached list.** The list page is detached from the tree after push and attached again on pop. That the cells are then still in the same place with the same handlers is not verified on iOS or Android (§10).

---

## 8. Sheets, tabs and lightbox

- **Tabs.** Every tab has its own region; flights stay within it and the tab bar stays in place, as in iOS 18's Photos. No tab switch is animated with hero.
- **Region → sheet, v1: no.** The sheet is another window (Android), another view controller (iOS) or a popup (Windows); a Spine-drawn flight would need an overlay at window level. The sheet opens as today.
- **Region → sheet on iOS 18+, step 2: native `.zoom`.** The sheet's root has `Hero.Tag`; `DisplayBottomSheet` sets `sheetVc.PreferredTransition = UIViewControllerTransition.Zoom(options, ctx => the source view's UIView)` before `PresentViewController`, and looks the source up per tag every time the provider is called. Questions for the spike: does the zoom's drag-to-dismiss respect `ModalInPresentation = true` and Spine's back guard, and does the forum's dimming jerk show up with Spine's detents?
- **The sheet's own stack.** Pages that are pushed inside a sheet go through the sheet's `NavigationRegion`; there C works exactly as in a root region.
- **Lightbox (#311), "the lightbox opens from its thumbnail".** The natural form on iOS is a full-screen presentation with `.zoom` from the thumbnail: opening, drag-down-to-dismiss and zooming back to the right thumbnail after paging (the provider's `ZoomedViewController` says which image is showing) come from UIKit, and `InteractiveDismissShouldBegin` blocks the dismissal while the image is zoomed in. On Android and Windows the lightbox opens with C's flight from the thumbnail to full screen. So #304 delivers the lightbox's opening; the lightbox delivers #304's first full-screen presentation.

---

## 9. Platforms

| Platform | Shared element (region) | Zoom (region) | Interactive back | Sheet / lightbox |
|---|---|---|---|---|
| iOS 18+, iPadOS 18+ | C | C | The edge pan | Native `.zoom` (step 2) |
| iOS 15–17 | C | C | The edge pan | The sheet as today; lightbox with C |
| Mac Catalyst | C (not verified) | C (not verified) | The pan exists, trackpad not tried | `.zoom` falls back to the default according to Apple; the sheet as today |
| Android 14+ | C | C | Predictive back through `HandleOnBackProgressed` | The sheet as today; lightbox with C |
| Android < 14 | C | C | No progress gesture; flight on release | As above |
| Windows | `ConnectedAnimation` or C (the spike decides) | C | — | The sheet as today; lightbox with C |
| Reduce Motion / animations off | Fade | Fade | Fade | UIKit's own behavior (not verified) |

---

## 10. The spike: what it must show

A Mac with Xcode, an iPhone (Jonatan's iPhone 16 Pro, iOS 26.5) and an Android 14+ device; Windows separately. A list (`CollectionView` with image cards) and a detail page with a `HeroCollectionView` header, in an Orientera-like shape.

1. **Layout timing.** How long from `FrontView.Content = next` until the destination view's rectangle can be measured — with and without `StateView`, with `HeroCollectionView` as the destination? Is a window of 100 ms enough?
2. **The way back.** When the list page is attached to `BackView` again: is the cell still there with the same handler and position, or is it realized anew? How long before the rectangle is right?
3. **Snapshot.** iOS: `SnapshotView(afterScreenUpdates: false)` on a cell with a MAUI `Image` — the right content, or empty? Android: `View.Draw` to a `Bitmap` on a Glide-loaded image — an exception for a hardware bitmap? Does `PixelCopy` work instead?
4. **Frame rate.** Does a MAUI-ticked animation (`Animation.Commit`, which the back swipe uses) hold 120 Hz on ProMotion while the destination page's `OnAppearingAsync` loads data? Compare with the same flight in `UIViewPropertyAnimator` and `ValueAnimator` respectively. The answer decides whether the choreography should live in MAUI or in a thin platform engine.
5. **Clip and scale.** How expensive is a `RoundRectangleGeometry` clip updated per frame on a full-screen presenter (the zoom), on iOS and Android?
6. **Interactive zoom.** The edge pan scrubs the zoom; release past and below the threshold; the back guard says no midway.
7. **Predictive back.** Does `HandleOnBackProgressed` reach Spine's callback with the app's targetSdk and manifest? Is `BottomSheetDialog`'s own back affected?
8. **Native zoom on the sheet.** `PreferredTransition` on `SpineSheetViewController`: opening from a tag on the region page, dismissing back to it, drag-to-dismiss with `ModalInPresentation = true`, detents medium and large.
9. **Windows.** `PrepareToAnimate`/`TryStart` between two MAUI-handled `FrameworkElement`s in the same window, within 250 ms despite the wait for layout.
10. **The header bar.** Should the flight go above or below the header bar, and what do page actions look like when they are swapped during a zoom (they already fade in and out, `PageActionView.cs:104-113`)?

---

## 11. What cannot be done, and what is not verified

**Nothing in this study has been tried.** It was done in a Linux container without a Mac, Windows, a simulator or a device, and without .NET workloads; not a line of Spine code and not a single platform API has been run. The evidence is Spine's source code, Apple's, Google's and Microsoft's documentation and the binding sources in `dotnet/macios`, `dotnet/maui` and `material-components-android`.

Cannot be done, according to the documentation and the code:

- iOS's `.zoom` for region push, unless Spine puts every page in a `UINavigationController`.
- `.zoom` on the Mac: Apple falls back to the default transition outside iPhone and iPad.
- `MaterialContainerTransform` that follows the predictive back gesture (not scrubbable).
- A Spine-drawn flight from a region page into a sheet on Android (two windows).

Not verified, and decisive: everything in §10, especially the layout timing (1, 2), the snapshots (3) and the frame rate (4). In addition: that the bindings in §3.1 exist in the repo's Microsoft.iOS 26.2 (they exist in `main`), that `ConnectedAnimationService` works in MAUI's WinUI window, that the net10.0-android apps in the repo actually have targetSdk 36, and how UIKit's zoom behaves with Reduce Motion.

---

## 12. Delivery plan

1. **The spike** (§10) on a scratch branch, iOS and Android first. The result decides between a MAUI engine and a platform engine, and the time window for layout.
2. **Issue with plan, and changelog** (`issues/304-…md`). The owner's decisions first: the tag's position as the mode selector (§5.1), the name, the header bar ordering.
3. **Shared element, non-interactive**, iOS and Android: `Hero`, `HeroRegistry`, the overlay layer and the wait for layout in `NavigationRegion`, the `Image` special case, Reduce Motion. Sample: list → detail in `MauiSpineSampleApp`.
4. **Zoom** (tag on the destination page's root) and **interactive back** through the edge pan on iOS.
5. **Android predictive back** in `SpineRegionBackCallback` — for hero and, in an issue of its own, for ordinary pops.
6. **iOS 18+ native zoom on sheets**, then the lightbox (#311) on the same foundation.
7. **Windows:** `ConnectedAnimation` or C, after measuring on Windows.
8. **Documentation:** `docs/wiki/custom-transitions.md` (new members), a wiki page about `Hero.Tag`, the `spine-page` skill. The study then becomes history.

---

## Decisions (2026-09-30)

Jonatan went through the study's questions on 2026-09-30 and followed the recommendations. Lines marked **Proposal** had no recommendation in the study; there a proposal is given with its reasons, and it applies until he says otherwise.

- **Engine.** Spine draws the transition itself for region pages (alternative C). Native `.zoom` (iOS 18+) is used only for sheets and the lightbox (alternative D).
- **Mode.** The tag's position selects the mode: on the destination page's root it is zoom, on a view inside it is shared element (§5.1).
- **No parameter on `NavigateToAsync`.** The transition is turned off with a switch in `SpineOptions.Transitions.SharedElements` or with `Tag = null`.
- **The name** — **Proposal.** `Transition.Tag` instead of `Hero.Tag`. "Hero" already means the collapsing header in Spine (`HeroCollectionView`), and two meanings of the same word in the same XAML become hard to read. `Transition` belongs with `ISpineTransitions` and covers both zoom and shared element. The study's text still says `Hero.Tag`.
- **The header bar** — **Proposal.** The flight goes below the header bar, as in §5.2 step 5. The bar floats over the content at rest (overlay, Liquid Glass), so an image that lands below it ends up directly in its final position; above the bar, the bar would pop up on top of the image when the snapshot is removed. The spike confirms.
- **`ISpineTransitions`.** Two new members with a default implementation: `AnimateSharedAsync` and `AnimateZoomAsync` (§5.4).
- **The back guard** — **Proposal.** `OnBackRequestedAsync` is asked as today, when the drag is completed. The back guard is asynchronous and may show a dialog, which cannot be waited for when a finger has just begun to drag. If the spike shows that a denied zoom looks wrong when the page springs back, a page with a back guard gets the ordinary reveal gesture instead of the zoom's.
- **Android predictive back.** A separate issue for ordinary pops, apart from #304.
- **Windows.** `ConnectedAnimationService` if the spike shows that it starts in time, otherwise Spine's own flight.
- **Region to sheet.** Not in v1. Native `.zoom` on iOS 18+ becomes step 2.
- **Drag down to dismiss.** Not in v1.
- **The order.** The spike (§10, iOS and Android first) runs before the issue plan.

---

## 13. References

- Apple, *Enhancing your app with fluid transitions*: https://developer.apple.com/documentation/uikit/enhancing-your-app-with-fluid-transitions
- Apple, `UIViewController.preferredTransition`: https://developer.apple.com/documentation/uikit/uiviewcontroller/preferredtransition
- Apple, `UIViewController.Transition`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition
- Apple, `zoom(options:sourceViewProvider:)`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoom(options:sourceviewprovider:)
- Apple, `zoom(options:sourceBarButtonItemProvider:)` (iOS 26): https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoom(options:sourcebarbuttonitemprovider:)
- Apple, `UIViewController.Transition.ZoomOptions`: https://developer.apple.com/documentation/uikit/uiviewcontroller/transition/zoomoptions
- Apple, *Enhance your UI animations and transitions* (WWDC24, session 10145): https://developer.apple.com/videos/play/wwdc2024/10145/
- Apple forum 788257, zooming into a sheet with a medium detent: https://developer.apple.com/forums/thread/788257
- dotnet/macios, the UIKit bindings (`PreferredTransition`, `UIViewControllerTransition.Zoom`, `UIZoomTransitionOptions`): https://github.com/dotnet/macios/blob/main/src/uikit.cs
- Android, *Add support for predictive back animations* (Views, `HandleOnBackProgressed`, `controlDelayedTransition`): https://developer.android.com/guide/navigation/custom-back/support-animations-views
- Android, *Predictive back gesture*: https://developer.android.com/guide/navigation/custom-back/predictive-back-gesture
- Android 16, behavior changes (predictive back for targetSdk 36): https://developer.android.com/about/versions/16/behavior-changes-16
- Material Components, *Motion* (container transform, "Transition between Views"): https://github.com/material-components/material-components-android/blob/master/docs/theming/Motion.md
- Material Components, `MaterialContainerTransform.java`: https://github.com/material-components/material-components-android/blob/master/lib/java/com/google/android/material/transition/MaterialContainerTransform.java
- Material Components, *Predictive Back*: https://github.com/material-components/material-components-android/blob/master/docs/foundations/PredictiveBack.md
- AndroidX, `Transition.java` (`isSeekingSupported`): https://github.com/androidx/androidx/blob/androidx-main/transition/transition/src/main/java/androidx/transition/Transition.java
- dotnet/maui 10.0.50, AndroidX versions: https://github.com/dotnet/maui/blob/10.0.50/eng/AndroidX.targets
- dotnet/maui, the Glide integration on Android: https://github.com/dotnet/maui/tree/main/src/Core/AndroidNative/maui/src/main/java/com/microsoft/maui/glide
- dotnet/maui, `GetBoundingBox` (internal): https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/ViewExtensions.cs
- Microsoft, *Connected animation* (the source in windows-dev-docs): https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/develop/motion/connected-animation.md
- Microsoft, WinRT APIs in desktop apps: https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/desktop/modernize/winrt-api-desktop-app-support.md
- WinUI 3 Gallery, connected animation within one page: https://github.com/microsoft/WinUI-Gallery/blob/main/WinUIGallery/SampleSupport/SamplePages/CardPage.xaml.cs
- Spine: `docs/wiki/custom-transitions.md`, `docs/wiki/regions.md` (back swipe), `docs/wiki/sheets.md`, `docs/wiki/tab-host.md`, `docs/proposals/spine-tab-host.md`, `issues/267-back-swipe-pan-vertical-drags.md`, `issues/428-navigation-showasync-go-to-a-page-without-stacking.md`, `issues/60-back-from-sheet.md`
