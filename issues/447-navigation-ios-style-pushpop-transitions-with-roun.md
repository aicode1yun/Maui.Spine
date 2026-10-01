# Issue #447 — Navigation: iOS-style push/pop transitions with rounded corners on iOS 26 and a native animation engine

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/447
**Branch:** issue/447-navigation-ios-style-pushpop-transitions-with-roun
**Status:** Completed

## Plan

1. **Layers, not pages.** Push and pop move the two content hosts in `NavigationRegion` (front and back) and the dim overlay between them, as the back-swipe already does. The page presenters inside the hosts are inset by the safe area, so moving them would leave the status-bar strip behind.
2. **`ISpineTransitions` gains `AnimatePushAsync` / `AnimatePopAsync(SpineTransitionContext)`.** The context carries `Front` (the upper layer: incoming on push, leaving on pop), `Back`, `Dim` and `Width`. Default interface implementations call the old per-page methods, so a direct implementer keeps today's behaviour. `DefaultSpineTransitions` overrides them with the iOS motion: the front layer slides over the back, which moves between 0 and −25 % while the dim fades.
3. **`NavigationRegionViewModel` hands the transition to the region** through a callback (direction + a commit action that swaps the contents), so the region can reset its layers only after the swap and nothing flashes.
4. **The front layer carries its page's background** during a transition and a swipe (`PagePresenter.PageBackground()`), on every platform; afterwards it goes back to `Colors.Transparent` (clearing the value leaves the native colour behind on iOS). With an opaque front layer the back-swipe no longer needs its per-frame clip of the previous page, and the dim overlay no longer needs to move with the back layer.
5. **Rounded corners on iOS 26+**: the front host gets `UICornerConfiguration` with `UICornerRadius.CreateContainerConcentric()` (the display's radius) and clips to it while it moves. Square elsewhere.
6. **Native engine on iOS / Mac Catalyst.** New `SpineTranslateToAsync` / `SpineFadeToAsync` extensions set `TranslationX/Y` / `Opacity` inside a `UIViewPropertyAnimator`, so Core Animation runs the motion off the main thread; MAUI's easings map to cubic timing curves, and an easing UIKit cannot express falls back to MAUI's ticker. Android and Windows use `TranslateToAsync` / `FadeToAsync` as today. `DefaultSpineTransitions` uses the helpers for every motion, including the swipe's release.
7. **Docs:** `docs/wiki/custom-transitions.md` describes the layer methods and the helpers.

## Open Questions

## Changes

- Back-swipe on iOS 26+: the dragged page is rounded to the screen's corners (`NavigationRegion.Apple.cs`, `RoundFront`).
- Back-swipe: the previous page and the dim reach the bottom of the screen. The clip that stopped short is gone altogether: the front layer now carries its page's background (`NavigationRegion.LiftFront`), so nothing needs clipping, and the dim no longer moves with the back layer.
- `SpineAnimation.SpineTranslateToAsync` / `SpineFadeToAsync` (`Extensions/SpineAnimation*.cs`): `UIViewPropertyAnimator` with MAUI's easings as cubic curves on iOS / Mac Catalyst, MAUI's ticker elsewhere.
- `ISpineTransitions.AnimatePushAsync` / `AnimatePopAsync` with `SpineTransitionContext` (`Front`, `Back`, `Dim`, `Width`); default implementations call the per-page methods after `Flatten()`.
- `DefaultSpineTransitions`: iOS-style push and pop on every platform, swipe release on the native engine; `Parallax` and `DimOpacity` overridable; per-page methods `[Obsolete]`.
- `NavigationRegionViewModel` hands push, back and pop-to to the region (`PlayTransition`, with a commit action that swaps the pages before the layers return to rest).
- `docs/wiki/custom-transitions.md` rewritten for layers and the helpers.
- In a sheet the back host sits in a layer (`_backLayer`) that is cut off at the front page's leading edge while a page moves (`CutBackAt`, driven by the front host's `TranslationX`): on iOS a `MaskView` whose frame is set inside the same UIKit animation, elsewhere a `RectangleGeometry` clip. No dim in a sheet.
- In an iOS 26 sheet the front layer gets a mask with the sheet's corner radius over the part of it inside the sheet (`SheetShape`): the layer reaches ~59 pt above the sheet, so its own corners fell outside it and the visible corners were square. It stays see-through: a glass pane of its own (tried in `Regular` and `Clear`) looked too light on the phone, glass doubled on the sheet's glass.
- `BottomSheetPageExtensions.Apple`: `PreferredCornerRadius = 20` only before iOS 26; iOS 26 rounds sheets concentrically with the screen, which the fixed 20 pt made tighter than system sheets.
- Verified in the iOS 26 simulator (iPhone 17 Pro): push, back button and swipe, recorded at 60 fps; dark mode and a theme change after a swipe. Not yet verified on Android or Windows.

## Decisions

- **Display radius from `containerConcentric`, not `_displayCornerRadius`.** The private key works but is private API; the concentric radius resolves to the same 62 pt on an iPhone 17 Pro.
- **Old per-page methods stay on the interface** (default implementations of the new ones call them) so a direct `ISpineTransitions` implementer keeps working. On `DefaultSpineTransitions` they are marked `[Obsolete]`: Spine no longer calls them there, and an override of one would otherwise be ignored silently.
- **The front layer's colour is reset to `Colors.Transparent`, not cleared.** Clearing `BackgroundColor` left the native colour on iOS, so after a swipe in dark mode the page stayed dark when the theme changed to light.
- **The region plays the transition, the view model only asks for it.** The layers (hosts and dim) belong to the region; moving the page presenters instead would leave the status-bar strip behind, since the presenters are inset by the safe area.
- **Sheets: cut the page underneath, front stays see-through.** An iOS 26 sheet is Liquid Glass (its view controller's background is clear and UIKit draws the glass), so a solid colour on the moving page showed as a white card on the glass, and a cross-fade of see-through pages looked muddled. A real `UINavigationController` in a glass sheet, recorded in the simulator, slides the new page in rounded like the sheet and shows nothing of the page underneath inside it: the lower page is cut at the moving edge, and the new page shows the sheet's own glass. The cut sits on a layer that does not move, so it depends on the front page alone and can be set from the front host's `TranslationX` change, inside the same animation on iOS.
