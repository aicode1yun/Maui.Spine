# Issue #303 — Feature idea: Haptics vocabulary — Success, Warning, Error, Selection and Impact

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/303
**Branch:** issue/303-feature-idea-haptics-vocabulary-success-warning-er
**Status:** In Progress
**Backlog:** P1 in #317

## Plan

### Gap
MAUI's `HapticFeedback` has only `Click` and `LongPress`. Both platforms have semantic generators (success, warning, error, selection, impacts of several weights) that follow the user's system haptics setting. Spine owns the places where system apps give haptic feedback (tabs, sheets, header actions), so the vocabulary belongs in the core package.

### API (`Plugin.Maui.Spine`, namespace `Plugin.Maui.Spine.Extensions`, next to `Tap` and `Glass`)
- `enum Haptic { None, Selection, Success, Warning, Error, Light, Medium, Heavy, Soft, Rigid }`: one value that covers everything. `OnTap`, `PageAction.Haptic` and the options take this type.
- `enum HapticImpact { Light, Medium, Heavy, Soft, Rigid }` for the shortcut from the issue sketch.
- `public static partial class Haptics`:
  - `Play(Haptic)` is the single entry point and calls `static partial void PlayPlatform(Haptic)`.
  - The shortcuts `Success()`, `Warning()`, `Error()`, `Selection()` and `Impact(HapticImpact)` all forward to `Play`.
  - `OnTap` is an attached property (`BindableProperty.CreateAttached`, `Haptic`, default `None`).
  - Internally, `Prepare(Haptic)` gets the iOS generator ready ahead of a known interaction. It runs on press-down, so the buzz does not lag the tap.
- The shape follows `StatusBar` (a static partial class with a partial platform method). It has no DI service, because it has no state that an app would want to replace.

### Where `OnTap` fires
- **`Button` and `ImageButton`:** plays on `Clicked` and prepares on `Pressed`. The subscription is made and removed in the property-changed callback.
- **Views with `Tap.Command`:** `TapState.Execute()` reads `Haptics.GetOnTap(view)` and plays it. The press-down paths (Apple `PressRecognizer` began, Android touch down, Windows pointer pressed) call `Prepare`. This covers every tappable Spine row and card without a second gesture recognizer.
- **Other views:** `OnTap` does nothing. This is documented, because adding a tap gesture of our own would compete with the view's own gestures.
- **`PageAction.Haptic`** (observable, default `None`): `PageActionView` forwards it to the `Button` or `ImageButton` it creates through `Haptics.SetOnTap`.

### Opt-in defaults: `SpineOptions.Haptics` (new `SpineHapticsOptions`)
- `TabSwitch`, a `Haptic` defaulting to `None`, plays from `SpineTabbedHostPage.OnCurrentPageChangedCore` when the switch comes from the user. Programmatic `SwitchToAsync` stays silent (it has a flag around `CurrentPage =`).
- `SheetDetent`, a `Haptic` defaulting to `None`, plays when a sheet the user is dragging settles on a new detent:
  - **Apple:** export `sheetPresentationControllerDidChangeSelectedDetentIdentifier:` on `SpineSheetDelegate` in `BottomSheetPageExtensions.Apple.cs`. UIKit calls it only for user drags.
  - **Android:** in the `SheetStateCallback` `onStateChanged` lambda (`BottomSheetPageExtensions.Android.cs`), when a settled state differs from `lastSettledState` and the sheet is not hiding.
- Reorder lift and drop belong to #301. The options class gets that property when #301 lands, not now.
- The values are `Haptic` rather than `bool`, so an app can choose `Light` over `Selection`.
- `UseSpine` hands the options to `Haptics` through an internal static, as `ConfigurePlatform` does.

### Platforms
- **iOS:**
  - `UINotificationFeedbackGenerator` (Success, Warning, Error).
  - `UISelectionFeedbackGenerator`.
  - `UIImpactFeedbackGenerator` with the style Light, Medium, Heavy, Soft or Rigid (Soft and Rigid are iOS 13+).
  - Generators are created lazily, kept per kind, and called on the main thread. `Haptics.Apple.cs` is guarded by `#if IOS`.
- **Mac Catalyst:** no-op, because the generators are silent on the Mac.
- **Android:** two engines, chosen with `SpineOptions.Android.HapticEngine` (`AndroidHapticEngine.View`, the default, or `AndroidHapticEngine.Vibrator`).
  - **`View`:** `PerformHapticFeedback` on the current activity's decor view.
    - It needs no permission and follows the system "touch feedback" setting.
    - API 30+ uses `Confirm` (Success) and `Reject` (Error). Selection uses `ClockTick` (`SegmentTick` on 34+).
    - Impacts step from `KeyboardTap` through `VirtualKey` to `LongPress`. Older APIs get the nearest constant. The exact table goes in Decisions after testing on a device.
  - **`Vibrator`:** `VibrationEffect`.
    - It uses composition primitives on API 30+ when `AreAllPrimitivesSupported` (e.g. Success = `Click` + `Tick`, Error = `Thud`, Warning = two `LowTick`s). Otherwise it uses predefined effects on API 29+ (`Click`, `DoubleClick`, `HeavyClick`, `Tick`).
    - It needs `android.permission.VIBRATE` in the app's manifest. The package does not add that permission for the app.
    - When the permission is missing, or the device has no vibrator or amplitude control, it falls back to `View` and logs a warning once that names the missing permission (see the "make failures visible" rule).
  - The engine is kept only if it feels better than `View` on a device. If it does not, it is dropped before the PR and the reason goes in Decisions.
- **Windows:** no-op.

### Steps
1. `Haptic`, `HapticImpact`, `Haptics` (shared file), `SpineHapticsOptions`, and `AndroidHapticEngine` with its option.
2. Apple, Android (both engines) and the empty Mac Catalyst and Windows halves.
3. `OnTap`, hooked into Button/ImageButton and `TapState` (play plus prepare on press-down).
4. `PageAction.Haptic` and `PageActionView`.
5. Tab-switch and sheet-detent defaults (Apple delegate export, Android callback).
6. Sample `Pages/Haptics/HapticsPage` (three files):
   - a row per value that plays it;
   - a glass button with `OnTap="Selection"`;
   - a header page action with `Haptic = Light`;
   - a toggle for the Android engine;
   - an index row in `MainPage.ViewModel.cs`.
   - The Showcase turns on `TabSwitch` and `SheetDetent` in `MauiProgram.cs`.
7. Docs:
   - `docs/wiki/haptics.md`;
   - rows in the root README ("Core concepts" and "Documentation") and in the package README;
   - a line on `PageAction.Haptic` in `page-actions.md`;
   - a line on the options in `tab-host.md` and `sheets.md`;
   - a section in the `/spine-controls` skill and a line in `/spine-page`.
8. Build for iOS, Android and Mac Catalyst. Verify on the iPhone, because the simulator has no Taptic Engine and an emulator does not vibrate. Windows is covered by CI.

## Open Questions

None. There is no Android phone (Jonatan, 2026-09-28): Android is verified through the vibrator service's record (`dumpsys vibrator_manager`) on the emulator, and on a Samsung tablet if it has a vibrator.

## Changes

- `Extensions/Haptics.cs`: `Haptic`, `HapticImpact`, `Haptics` (`Play`, the shortcuts, internal `Prepare`, `OnTap` on `Button` and `ImageButton` through `Pressed`/`Clicked`). `Play` hops to the main thread.
- `Platforms/iOS/Haptics.iOS.cs`: lazily created notification, selection and per-style impact generators. Mac Catalyst and Windows have no platform half, so the partial methods vanish.
- `Platforms/Android/Haptics.Android.cs`: `PerformHapticFeedback` on the activity's decor view, or `VibrationEffect` (composition on 30+, predefined on 29, touch usage on 33+) with a one-time `Log.Warn` fallback when the permission, the API level or the vibrator is missing.
- `SpineOptions.Haptics` (`SpineHapticsOptions`: `TabSwitch`, `SheetDetent`), `SpineOptions.Android.HapticEngine` (`AndroidHapticEngine`); `UseSpine` hands the options to `Haptics`.
- `TapState.Execute` plays the view's `OnTap` before the command; the Apple press recognizer prepares it on touch-down.
- `PageAction.Haptic` and `[PageAction(Haptic = …)]`; `PageActionView` sets `OnTap` on both of its buttons (and the morphing glass button).
- `SpineTabbedHostPage`: plays `TabSwitch` when `CurrentPage` changes, unless `SwitchToAsync` set it.
- Sheets: Apple `SpineSheetDelegate` exports `sheetPresentationControllerDidChangeSelectedDetentIdentifier:`; Android plays when a drag settles on a different state than the last settled one.
- Android view engine remapped after the emulator showed `TEXT_HANDLE_MOVE` (Soft) as `ignored_unsupported`: Light and Soft use `CONTEXT_CLICK` (TICK), Medium and Rigid `VIRTUAL_KEY` (CLICK), Selection `CLOCK_TICK` (TEXTURE_TICK).
- Docs: `docs/wiki/haptics.md`; a section in `page-actions.md`, a line in `tab-host.md` and `sheets.md`; root README (feature list, Core concepts, Documentation; the Shortcuts and Menu buttons rows in Core concepts were merged into one broken row and are split again), package README; `/spine-controls` and `/spine-page` skills.
- Verified on the Pixel 10 Pro emulator (API 37) through `dumpsys vibrator_manager`:
  - View engine: Success → CONFIRM (CLICK), Warning → LONG_PRESS (HEAVY_CLICK), Error → REJECT (DOUBLE_CLICK), Selection → CLOCK_TICK (TEXTURE_TICK), Light and Soft → CONTEXT_CLICK (TICK), Medium and Rigid → VIRTUAL_KEY (CLICK), Heavy → LONG_PRESS (HEAVY_CLICK). All nine are recorded with usage TOUCH.
  - Vibrator engine: all nine are played as the composed primitives in the table, with usage TOUCH.
  - The Save action (`[PageAction(Haptic = Success)]`) plays CONFIRM, and the Follow button (`OnTap="Selection"`) plays CLOCK_TICK.
  - Sheet: a drag from 50 % to full screen and one from full screen to 75 % each play once. A short drag that springs back plays nothing.
  - Not verified: the fallback warning when the permission is missing, because the sample declares it.
- Sample: `Pages/Haptics/HapticsPage` (rows for every value, a glass button with `OnTap`, a Save action with `Haptic = Success`, the tab and sheet options, an Android engine switch), index row, csproj and xmlns entries, `TabSwitch`/`SheetDetent` on in `MauiProgram`, `VIBRATE` in the Android manifest.

## Decisions

- One enum `Haptic` for everything, plus the shortcuts from the issue (`Success()` … `Impact(HapticImpact)`) on top of `Play(Haptic)` (Jonatan, 2026-09-28). `OnTap`, `PageAction` and the options need a single value that covers both the notification kinds and the impacts.
- Android supports both engines as a choice, with `View` as the default (Jonatan, 2026-09-28). `Vibrator` gives richer patterns but needs the VIBRATE permission and ignores the touch-feedback setting.
- The work runs in the worktree `../Maui.Spine-303`, because the main directory is shared with other sessions.
- `[PageAction]` got a `Haptic` too, not in the plan: declared actions are the common way to add a header button, and a Save that buzzes should not need code.
- `OnTap` plays before the command runs, so a command that navigates away or throws still gives its feedback, the way UIKit controls do.
- `UIImpactFeedbackGenerator(style)` is deprecated from iOS 17.5 in favour of a view-bound generator; the view only positions feedback on a trackpad, so the plain initializer stays with a suppressed CA1422.
- Android `Warning` has no semantic constant; it maps to `LongPress` (the heavier single) on the view engine and to a strong-then-weak double click on the vibrator.
- The view engine reaches only the prebaked effects (texture tick, tick, click, heavy click, double click), so on Android impacts share effects in pairs (Light = Soft, Medium = Rigid). Keeping five distinct impacts is what `Vibrator` is for, which is why both engines stay. `TEXT_HANDLE_MOVE` is dropped because the vibrator service ignores it outside a text-handle drag.
- The sample removes the base class's theme menu, as `PageActionsPage` does, because the header has one trailing slot and the Save action has to be visible.
