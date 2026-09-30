# Issue #316 — Small delights: rolling numbers in AnimatedLabel

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/316
**Branch:** issue/316-rolling-numbers
**Status:** Completed

The issue has two halves. This branch takes **rolling numbers**. Typed action sheets with icons
(`ShowActionsAsync`) are native work per platform and are done separately.

## Plan

`Mode="RollingNumber"` on `AnimatedLabel`: when the text changes, the characters that differ roll
vertically, like an odometer. The rest stand still. With Reduce Motion it is a fade, the same as the
control's ordinary text change.

1. **`RollingNumber.cs`**, without MAUI types. It works out which characters belong together, which
   way they roll and where they stand, and draws one frame with SkiaSharp. Having no MAUI types, it
   can be linked into a `net10.0` test project, the way `Svg.Tests` and `Core.Tests` do.
   - **Pairing:** see Decisions; it changed during verification.
   - **Direction:** the number grows → the old leaves upwards and the new comes from below. When it
     shrinks it goes the other way. The number is read with the app's culture and otherwise
     invariantly. If neither works (`12:59`) the digits are compared in order.
2. **`AnimatedLabel`:** `Mode` (`Marquee`, the default, and `RollingNumber`) and `RollDurationMs`.
   In `RollingNumber` mode the text does not scroll sideways (numbers are short). The roll runs on
   the existing shared ticker, and when it is done the same pre-rendered image as today is drawn.
3. **Tests** in a new `tests/Plugin.Maui.Spine.Controls.Tests` for pairing and direction. They run
   in CI.
4. **Sample:** a third example on the Marquee page, with a number that counts up and down.
5. **Wiki:** `docs/wiki/animated-label.md`.

## Changes

- `RollingNumber.cs`: pairing (`Pair`), direction (`Increases`), easing, and the drawing of one
  frame. Owns its `SKFont` and its `SKPaint`.
- `AnimatedLabel`: `Mode` (`AnimatedLabelMode.Marquee` / `RollingNumber`) and `RollDurationMs` (350).
  The roll runs on the shared ticker. A change in the middle of a roll starts over from the latest
  value. In rolling mode there is no marquee and no edge fades. `CreateFont()` is split out of
  `RebuildBuffer` so that the roll and the image draw with the same font.
- `ReduceMotion.cs` in the package, the same shape as in Shimmer and Scanner. With Reduce Motion on,
  the ordinary fade (or an immediate change) is used.
- `tests/Plugin.Maui.Spine.Controls.Tests`: tests for pairing, direction, easing, tabular layout and
  alignment. Added to `Spine.slnx`, `Spine.Packages.slnf` and CI.
- The sample: the example "Numbers that roll" on the Marquee page, with a score (−1, +1, +95), a
  countdown from 0:15, and options for alignment and duration.
- Documentation: `docs/wiki/animated-label.md`, the package README and csproj description, and the
  `spine-controls` skill.

Found and changed while verifying on devices (2026-09-30):

- **Half-pixel hand-over fixed.** `OnPaintSurface` centred the text at `(height − textHeight) / 2`,
  which is a half pixel when the difference is odd (25.5 px on the iPhone 17 Pro). The pre-rendered
  image was then resampled and blurred, while the roll's glyphs snapped to the pixel grid, so the
  text shifted half a pixel and changed sharpness when a roll ended. Both paths now use whole
  pixels (`MathF.Floor`). This also makes still text in Marquee mode sharper.
- **Pairing from the left** (Jonatan, after seeing it run): `99` → `100` is 9 → 1, 9 → 0 and a new
  0 that rolls in at the end. Right-aligned pairing made the two nines slide sideways.
- **Tabular figures** (Jonatan): in rolling mode every digit gets a cell as wide as the font's widest
  digit and is centred in it. The image at rest is drawn by the same code as the last frame of a
  roll (`RollingNumber.DrawLine`), so the two are the same pixels by construction.
- **`HorizontalTextAlignment`** (Jonatan): `Start`, `Center`, `End`, in both modes. With `End` the
  pairing is from the right, so the ones stay over the ones. `OnModeChanged` now rebuilds the image,
  since the two modes lay the text out differently.

## Verified on device

Builds (this branch, 2026-09-30, .NET SDK 10.0.201, Xcode 26.2): `Spine.Packages.slnf` for
net10.0-ios, net10.0-maccatalyst and net10.0-android, 0 errors and no new warnings against master;
`samples/MauiSpineSampleApp` for the iOS simulator, Mac Catalyst and Android, 0 errors. The sample's
XAML (`AddCommand`, `CountDownCommand`, `ShowRollOptionsCommand`, `Mode="RollingNumber"`,
`HorizontalTextAlignment`) compiles with the XAML source generator. Tests: Server 225, Svg 24,
Core 29, Barcodes 35, Controls 35, all passing.

Devices: iPhone 17 Pro simulator (iOS 26.2, 3×), Pixel 10 Pro emulator (Android 17, 3×), Mac Catalyst
on macOS 27.0 (built-in 2× display and an external 1× display). The sample was driven by a test
harness compiled in from outside the repo; motion was judged from 60 fps screen recordings.

1. `98 → 99 → 100`: only the digits that change roll; the new digit arrives at the end (start
   alignment) or at the front (end alignment). iOS, Android, Mac.
2. `−1` rolls the other way, and so does the countdown; the colon stands still. iOS, Android.
3. `+95` (`98 → 193`): one roll per column, no spinning. iOS, Android.
4. Eight changes 90 ms apart and six 40 ms apart, in the middle of rolls: no crash, no flicker, each
   roll starts from the latest value. iOS, Android.
5. Hand-over at the end of a roll: the last drawn frame and the pre-rendered image are pixel
   identical, both in an in-app render of the two paths and in screenshots of a roll held at its
   last frame against the settled label, for all three alignments. iOS (3×), Android (3×), Mac (1×
   and 2×). Before the fix, 394 pixels differed on iOS.
6. Reduce Motion (iOS) and Remove animations (Android): the number fades, it does not roll.
7. Dark mode: the rolling glyphs have the label's colour, with a fixed `TextColor` and with the
   default one. iOS, Android.
8. Mac Catalyst: the window moved between the 1× and the 2× display in the middle of a roll, both
   ways. The roll ends, the image is rebuilt at the new scale, no artefacts.
9. Durations from 100 to 1500 ms roll in the time they say. 350 ms reads as about 17 frames of
   visible motion with the ease-out; it stays the default.
10. The marquee examples on the same page work as before: scrolling, fade on change, start over.

**Not verified:**

- Windows (build and run). CI builds it when it is enabled again.
- A physical phone. Frame pacing on a real device was not measured.
- The options sheet's slider and chips were not dragged or tapped by hand; the values were set
  through the view model.

## Decisions

- **`Mode` instead of a `TextChangeAnimation` property.** The issue's sketch has
  `Mode="RollingNumber"`, and marquee and rolling do not go together: a number that rolls should not
  glide sideways at the same time.
- **No marquee in rolling mode.** Numbers are short. Text that is still too wide is clipped, and
  the wiki says so.
- **Slide in and out, not through the digits in between.** `9 → 3` swaps one digit for another
  instead of spinning past 0, 1 and 2, the way SwiftUI's `numericText` does. Large jumps (`+95`)
  would otherwise be long spins, and with values that update often nothing could be read.
- **Pairing follows the alignment.** The text is anchored where it is aligned, so that is where the
  columns stay put: from the left for `Start` and `Center`, from the right for `End`. This replaces
  the first version's right-aligned pairing, which moved digits sideways in a start-aligned label.
- **Each run of digits is paired on its own** when both texts are built the same way (the same
  sequence of digit runs and other runs). Otherwise `9:59` → `10:00` paired from the left would
  roll the colon. Texts built differently (`5` → `5 pts`) are paired as a whole.
- **Tabular figures by cell, not by font feature.** SkiaSharp's `DrawText` does not shape with
  OpenType features, and HarfBuzz would be a new dependency. A cell as wide as the widest digit
  works with any font.
- **Tabular figures only in rolling mode.** Marquee text keeps the font's own spacing, so existing
  labels do not change width.
- **A character that arrives or leaves has one place.** It rolls where it ends up (or where it
  was), and does not glide with the columns beside it.
- **The direction is taken from the number, not per digit.** `9 → 10` should roll upwards in every
  column.
- **No new package and no `Spine` reference.** AnimatedLabel should keep working without Spine, so
  Reduce Motion is read in the package itself.
- **The tests in a new `Plugin.Maui.Spine.Controls.Tests`** and not in `Core.Tests`, which tests
  `Plugin.Maui.Spine`. More control packages can put their MAUI-free logic there.
