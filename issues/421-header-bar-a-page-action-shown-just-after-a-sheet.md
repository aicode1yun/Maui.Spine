# Issue #421 — Header bar: a page action shown just after a sheet opens can be missed (implicit close button stays away)

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/421
**Branch:** issue/421-header-bar-a-page-action-shown-just-after-a-sheet
**Status:** Completed

## Plan

1. **Reproduce first, report before fixing.** A Showcase test sheet whose view model declares a secondary
   action with `IsVisible = false` and shows it at a chosen moment: synchronously in `OnAppearingAsync`, from a
   `Task.Delay(0)` continuation on the main thread, and after ~300 ms (the camera case from #415). A harness
   injected from outside the repo (see the sample test harness) opens and closes it 20 times per timing on the
   iOS simulator and the Android emulator and, for each open, records both:
   - the region view model's resolved slots (`PrimaryPageAction`, `SecondaryPageAction`), and
   - what `HeaderBarView` actually shows (`IsVisible`, `Opacity`, `Scale` of each `PageActionView`, and on iOS the
     alpha of the glass content in UIKit),
   so a miss can be placed either in the view model (the issue's hypothesis) or in the show/hide animation.
2. **Fix in the core.** First planned in `NavigationRegionViewModel` (follow `HeaderRegionViewModel` and re-read the
   slots). The reproduction below put the cause in `HeaderBarView.AnimateVisibility` instead, so the fix went
   there; the subscription is left as it is.
3. **Verify** with the same run (0 misses on both platforms), and check the Sheets page, the options sheet and the
   scanner sheet.
4. **Ask** whether `TorchSupport` in `Plugin.Maui.Spine.Scanner` (the #420 workaround) should stay.

Note from reading the code: `NavigationService` calls `SendAppearingAsync` *before* the sheet region's
`ResetAsync`, and `ResetAsync` re-reads the slots after setting `FrontView.Content`, which also moves the
subscription. So a synchronous change in `OnAppearingAsync` should already be picked up by the view model. The
repro is meant to tell whether the miss is there at all, or in `HeaderBarView.AnimateVisibility` (a hide that is
still running, or glass content left at alpha 0 by `GlassAppearance`, when the close button moves slots).

## Reproduction (before the fix)

A harness (outside the repo) opened and closed the test sheet and sampled the header 1.5 s and 3 s after
each open: the region view model's slots, the header bar's bound actions, and each `PageActionView`'s
`IsVisible` / `Opacity` / `Scale` plus its native view.

iOS simulator (iPhone 17 Pro, iOS 26.4):

| Shown | Misses |
|---|---|
| in `OnAppearingAsync` | 0 / 20 |
| after `Task.Delay(0)` | 0 / 20 |
| 300 ms into `OnAppearingAsync` | 0 / 20 |
| by the harness, 30–60 ms after the navigation starts | 4 / 5 |
| by the harness, 100 ms after | 5 / 5 |
| by the harness, 200–1200 ms after | 0 / 40 |

In every miss the view model was right: `PrimaryPageAction` was the close button, `SecondaryPageAction` the
share action, and the header bar was bound to both. The close button's view was stuck at `IsVisible = true`,
`Opacity = 0`, `Scale = 0.85`, three seconds later too: the end state of a hide.

Android emulator (Pixel 10 Pro, Android 17): 0 / 60 for the three sample timings and 0 / 55 for the
harness sweep (0–1200 ms). The page often arrived later there (the action was shown 40–540 ms after the
navigation started), but Android's show animates with `FadeToAsync(1)` / `ScaleToAsync(1)`, which replace the
hide's animations of the same name.

So the cause is not the subscription in `NavigationRegionViewModel` but `HeaderBarView.AnimateVisibility`:
- The sheet region still shows the previous sheet's actions (close leading, share trailing). `ResetAsync`
  resolves the new page's slots before it has its share action, so the leading slot hides (a 150 ms
  `FadeToAsync(0)` + `ScaleToAsync(0.85)` on iOS).
- The share action appears within those 150 ms, the close button moves back to the leading slot, and
  `AnimateVisibility(show)` runs. It aborts only an animation called `"Visibility"`, which nothing starts, so
  the hide's fade and scale keep running.
- With glass header actions the show sets `Opacity = 1` and `Scale = 1` directly, after one frame, without an
  animation of its own. The hide's fade then carries on to 0, and when it ends the version check (correctly)
  refuses to set `IsVisible = false`. The button is left visible, transparent and shrunk.
- Tapping the torch in #415 made it appear, because a new action ran the show again with nothing to fight.

The three timings the issue suggests all resolve the action before `ResetAsync` reads the slots, or after the
hide has finished, so they never hit the window. The camera's start in #415 landed in it on the iPhone.

## Verification (after the fix)

Same harness. The three timings from the issue now come from a copy of the test sheet inside the harness, since
the sample keeps one example.

| | iOS simulator | Android emulator |
|---|---|---|
| Shown 0, 50, 100, 150 ms after the navigation starts | 0 / 80 | 0 / 80 |
| In `OnAppearingAsync`, after `Task.Delay(0)`, 300 ms in | 0 / 60 | not finished¹ |
| Shown 200–1200 ms after | 0 / 45 | not finished¹ |

Control: the same build with the old line back (`AbortAnimation("Visibility")`) missed 14 of 15 on iOS at
0–100 ms.

¹ Another session took over the shared emulator partway through. These rows never missed on Android before the
fix either (0 / 115).

Checked visually on the iOS simulator: the Sheets page, the new example (share appears, X moves to the leading
side), the notifications edit sheet, the detents sheet, the scanner options sheet and the scanner sheet (X on
the trailing side; the simulator has no torch).

## Open Questions

None.

## Changes

- `HeaderBarView.AnimateVisibility` cancels the action's running animations (`CancelAnimations`) before it
  shows or hides it. It used to abort an animation called `"Visibility"`, which nothing starts, so a hide's
  `FadeToAsync(0)` / `ScaleToAsync` kept running under a show and left the action transparent.
- Showcase, Sheets page: new example "An action that shows up later" (`LateActionSheet`), a sheet whose share
  action is hidden until 300 ms into `OnAppearingAsync`.

## Decisions

- **Fixed the animation, not the subscription.** Across every miss, the view model resolved the right slots
  and the header bar was bound to them. `ResetAsync` already moves the subscription and re-reads the slots
  after the page is set, and `OnAppearingAsync` runs before that. Changing the subscription would fix nothing
  that was measured (agreed with Jonatan).
- **`CancelAnimations` rather than aborting `"FadeTo"` and `"ScaleTo"` by name.** It is MAUI's public way to stop
  every view animation, so it does not depend on the handle names MAUI uses internally.
- **One button in the sample.** The issue's timings never hit the window, which is 0–150 ms after the navigation
  starts, so three buttons would suggest a difference that isn't there. The sample shows the pattern (an
  action that appears later), and the harness covers the timing.
- **`TorchSupport` stays in the scanner.** After the fix the close button no longer depends on it, but deciding
  the torch before the sheet shows still keeps the close button from jumping sides under the user's finger
  once the camera starts (agreed with Jonatan). Its doc comment already gives only that reason.
