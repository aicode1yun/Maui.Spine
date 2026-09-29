# Issue #428 — Navigation: ShowAsync — go to a page without stacking a second copy

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/428
**Branch:** issue/428-navigation-showasync-go-to-a-page-without-stacking
**Status:** Completed

## Plan

- `INavigationService.ShowAsync<TPage>()` and `ShowAsync<TPage, TParam>(TParam)`.
- `NavigationRegionViewModel`:
  - `Find(Type)`: the topmost page of that type on the stack.
  - `IsTop(View)`.
  - `PopToAsync(View)`: closes every page above it in one back transition, with the same guard (`OnBackRequestedAsync` of the top page), pending-result cancellation and appearing and disappearing calls as `PopToRootAsync`, which becomes `PopToAsync(<root>)`.
- `NavigationService.ShowCoreAsync`, by the page's presentation:
  - **Tab:** as `NavigateToAsync`, which already switches to the tab and delivers the parameter.
  - **Region page on the current root stack:** the open sheet, if any, is closed first. The parameter goes to the existing view model through `OnNavigationParameterAsync`, then the region pops back to the page unless it is already on top.
  - **Sheet page on the open sheet's stack:** the same, within the sheet.
  - **Anything else:** pushed as `NavigateToAsync` does.
- A page already on top gets only the parameter; `OnAppearingAsync` does not run again, since nothing appeared.
- Sample:
  - `ShortcutHandler` and the widget link handlers (`SampleWidget`, `ScoreWidget`) use `ShowAsync`.
  - `BarcodesPageViewModel` opens the scanner from `OnNavigationParameterAsync` when the page is already showing.
- Docs:
  - `docs/wiki/navigation-parameters.md`, or the page that documents `NavigateToAsync`;
  - `shortcuts.md` and `widgets.md` ("Opening the app from the widget");
  - the `spine-page` skill.

## Open Questions

## Changes

- `INavigationService.ShowAsync<TPage>()` and `ShowAsync<TPage, TParam>(TParam)`, implemented in `NavigationService.ShowCoreAsync`.
- `NavigationRegionViewModel`: `Find(Type)`, `IsTop(View)`, and `PopToAsync(View)`, which `PopToRootAsync` now delegates to (one back transition, the same guard and pending-result cancellation).
- Samples: `ShortcutHandler`, `SampleWidget`, `ScoreWidget` and the push sample's `SamplePushHandler` use `ShowAsync`. `BarcodesPageViewModel` opens the scanner from `OnNavigationParameterAsync` when the page is already showing.
- Docs: `regions.md` ("Showing a page from outside the app"), `shortcuts.md`, `widgets.md`, `push-notifications.md`, the `spine-page` skill.

### Verified (2026-09-29, iPhone 17 Pro simulator, iOS 26.4, through the shortcut handler)

- Theme, Theme, Back → the main page: one Theme page, not two.
- Theme → Widgets → Scan a code (Barcodes with the scanner sheet open) → Theme: the sheet closes, Widgets and Barcodes are popped, Theme is in front; Back → the main page.

## Decisions

- **An open sheet closes before a region page is shown, found or pushed.** `NavigateToAsync` pushes region pages into the root region under an open sheet, where they cannot be seen; for a way in from outside, the page has to be in front.
- **A page already in front gets its parameter but no appearing.** Calling `OnAppearingAsync` for a page that never went away would run its loads again; the parameter hook is where to act.
- **Only the current region is searched.** With tabs, a page on another tab's stack is pushed onto the selected tab rather than switching tabs by itself.
