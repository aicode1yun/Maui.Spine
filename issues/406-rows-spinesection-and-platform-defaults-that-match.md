# Issue #406 — Rows: SpineSection and platform defaults that match iOS and Android Settings

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/406
**Branch:** issue/406-rows-spinesection-and-platform-defaults-that-match
**Status:** In Progress

## Plan

All in `src/Plugin.Maui.Spine.Controls.Rows` unless noted.

### 1. `SpineSection` (new: `SpineSection.cs`, `SpineSectionStyleOptions.cs`)
- A `ContentView` with `Header`, `Footer` (strings) and its rows as content (`[ContentProperty(nameof(Rows))]`, an `IList<View>`).
- Builds header label, a rounded group (`Border`, no stroke) and footer label.
- Inserts a separator between visible rows (not after the last), rebuilt when rows are added, removed or change visibility:
  - iOS / Mac Catalyst: a hairline (`1 / display density`), leading inset at the row's text column (asked from the row: padding + icon + spacing, or padding when it has no icon), trailing inset equal to the row's trailing padding (16), system `separator` colour.
  - Android: none (Material 3 lists use spacing). Windows: a hairline like iOS.
- `SpineSectionStyleOptions` (same `SpineStyleOptions<T>` chain as `SpineRowStyleOptions`, app-wide key `DefaultSpineSectionStyleOptions`): group background (`secondarySystemGroupedBackground`: white / #1C1C1E), corner radius, separator colour, header and footer font/colour, group margin. Theme defaults per platform.
- A public `SpineSection.GroupedBackground(AppTheme)` colour (#F2F2F7 / #000000 on iOS) for the page behind, documented; the section cannot paint the page itself.

### 2. `SpineRow` platform defaults
- **Chevron:** keep Spine's embedded `chevronleft.svg` (the same path as `ChevronRight.svg` in the icon set, mirrored; no new package dependency for Rows) and give it a heavier stroke with `SvgImageSource.LineWidthScale` (new `SpineRowStyleOptions.ChevronLineWidthScale`), sized to match `chevron.forward`.
- **Icon badge:** new `SpineRow.IconBackground` (Color). When set, the icon sits white on a filled shape: a ~30 pt rounded square (radius ~7) on iOS / Windows, a 40 dp circle on Android. Without it, the tinted glyph as today.
- **Row height:** iOS 26+ minimum 52 pt (measured 51.3 in Settings), 44 before; Android 56 dp for one line, 72 dp with a detail line (Material 3 list item).
- **Android value placement:** new `SpineRow.ValuePlacement` (`Auto`, `Trailing`, `Below`). `Auto` = below the title on Android (the Preference "summary"), trailing elsewhere. When below, it replaces the detail line if there is no detail, else it goes under the detail.
- **Android chevron:** `ShowChevron = null` resolves to no chevron on Android.
- **Android icon:** 24 dp (28 elsewhere as today).
- **Selection:** new `SpineRow.IsSelected` (bool?). `null` = not a selection row; `true` shows a check mark in the accent colour at the end (iOS, Windows) — a `check.svg` added to Spine's embedded SVGs next to `chevronleft.svg`; on Android a check mark as well (Material 3 single-choice lists also use a trailing check). The merged semantics announce "selected".
- **Dynamic Type / font scale:** verify labels scale (MAUI `FontAutoScalingEnabled`) and the row grows; fix min-height handling if it clips.

### 3. Sample and docs
- Rows sample page on `SpineSection`: a section with header and footer, badge icons, a selection list; page background from `SpineSection.GroupedBackground`.
- `docs/wiki/rows.md` and the package README: `SpineSection`, the new properties, the platform defaults.
- Side-by-side check against the Settings app on the iOS simulator and, if an emulator responds, Android.

### Not in this issue
- Hiding the separators next to a pressed row: needs the pressed state that is private to `Tap.Apple.cs`; a follow-up.
- Swipe actions, reordering, context menus; a native-backed list.

## Open Questions

## Changes

- `SpineSection` + `SpineSectionStyleOptions` (new): header, footer, a filled group (26 pt corners on iOS 26, 10 before, none on Android, 8 on Windows), hairline separators from the row's text to 16 pt before the edge, updated in place when a row hides or changes its icon. `PageBackgroundLight` / `PageBackgroundDark` give the iOS grouped background (transparent elsewhere).
- `SpineRow.IconBackground`: a 30 pt rounded square (iOS, Windows) or 40 dp circle (Android) behind a white icon.
- `SpineRow.ValuePlacement` (`Auto`, `Trailing`, `Below`); `Auto` puts the value under the title on Android.
- `SpineRow.IsSelected`: a check mark in the accent colour for pick-one lists; `check.svg` added to Spine's embedded SVGs; "Selected" / "Vald" read by screen readers (`Spine.Row.Selected`, the package's first strings).
- Chevron and check drawn with a heavier stroke (`SpineRowStyleOptions.GlyphLineWidthScale`, 1.6).
- `ShowChevron = null` shows no chevron on Android, nor on a choice row.
- Row height 52 pt on iOS 26 (measured 51.3 in Settings), 44 before; Android 56 / 72 dp for one / two lines (`TwoLineMinimumHeight`); Android icon 24 dp.
- Row spacing moved from `ColumnSpacing` to margins, so a row without a chevron ends at its padding (the grid kept the spacing of empty columns).
- Rows sample rebuilt on `SpineSection` (grouped background, badge icons, a pick-one list), with options for badges, value placement and `CanExecute`.
- `docs/wiki/rows.md` and the package README.

Verified on the iOS 26.4 simulator against the Settings app: group margins, 52 pt rows, 30 pt badges, separator from the text column to 16 pt before the edge, one device pixel thick. Android built; the emulators were unresponsive, so the Android look is from the Material 3 spec, not a device.

## Decisions

- Built on `sample/showcase-structure` (PR #407), so the Rows sample is reworked once, on the new page pattern.
- The platform look is the default: `ValuePlacement = Auto` and `ShowChevron = null` follow the platform, so on Android the value moves under the title and the chevron goes away when an app bumps the package. `Trailing` and `ShowChevron = true` restore today's look.
- `SpineSection` never re-parents its rows after a row property change: removing a row from the group resets its inherited `BindingContext`, so a bound `IconBackground` changed and triggered another rebuild, an endless loop found in the simulator. Rows are laid out once per collection change; property changes only update the separators.
- The badge glyph is drawn larger than the badge (34 in 30) and clipped: the icon set draws with a margin, so a glyph the badge's size looked tiny next to Settings.
- Separators stay visible while a row is pressed; hiding them needs `Tap`'s pressed state, which is private to `Tap.Apple.cs`. Left for later.
- The chevron stays Spine's own embedded glyph with a heavier stroke rather than `ChevronRight.svg` from Plugin.Maui.Spine.Svg.Icons: it is the same path mirrored, and Rows would otherwise depend on the icon package.
