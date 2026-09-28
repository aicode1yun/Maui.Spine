# Issue #418 — Sheets: confirm with a checkmark and cancel with an X (PageActionRole)

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/418
**Branch:** issue/418-sheets-confirm-with-a-checkmark-and-cancel-with-an
**Status:** Completed

## Plan
Following the iOS 26 Human Interface Guidelines, sheets confirm with a checkmark and cancel with an X, and Spine makes that the default and the recommendation. Decided with the maintainer:
- **A role, not a spelled-out icon:** `PageActionRole` (`Confirm`, `Cancel`) on `PageActionAttribute` and `PageAction`.
- **Android uses the checkmark too.**

## Open Questions

## Changes
- **`PageActionRole`** (new, `Plugin.Maui.Spine.Core`):
  - `Confirm` sets `check.svg` and `Cancel` sets `close.svg`.
  - Each sets a screen-reader `Description`: the action's text, or the new localised `Spine.Header.Done` / `Spine.Header.Cancel` (en: Done/Cancel, sv: Klar/Avbryt).
  - `Cancel` defaults to the `Primary` (leading) slot.
  - An explicit `Svg`, `Description` or `Placement` wins.
- **`PageAction.Role`, `PageActionAttribute.Role`;** `Placement` on both now falls back to the role's slot. `PageActionDiscovery` passes `Svg` before `Role`, so an attribute that names both keeps its icon.
- **Showcase:**
  - the edit sheet has Cancel/Save roles;
  - `SamplePage` and `OptionsSheet` have a Confirm role;
  - the Haptics page's Save has a Confirm role;
  - the Sheets and Haptics code samples, the menu text and the edit sheet's text are updated.
- **Push sample:** the tags sheet's Save has a Confirm role.
- **A `Confirm` action is drawn prominently.**
  - On iOS 26 with Liquid Glass it is `GlassStyle.Prominent`: glass tinted with the accent, the glyph in `SpineAccent.TextOn(accent)`.
  - Without glass (Android, older iOS) it is Material 3's filled icon button: a 40-point accent circle with its own visual states (fill, lighter while pressed, dimmed when disabled), because the common states' transparent background outranks a fill set on the button.
  - The fill follows theme and accent changes.
- **Docs:**
  - `sheets.md` (the button guidance);
  - `page-actions.md` (a new "Roles: confirm and cancel" section and the attribute table);
  - `page-pattern.md`, `haptics.md`;
  - the `spine-page` skill;
  - the XML doc examples on `PageAction`, `PageActionAttribute` and `ViewModelBase`.

## Decisions
- **The text stays in the code.** `[PageAction("Save", Role = PageActionRole.Confirm)]` keeps "Save" as what a screen reader says: an icon-only header button with no name is the accessibility bug this avoids. Without a text, the role's localised Done/Cancel is used.
- **Region pages get the role too** (the Haptics page's Save): a confirm in a header bar is a checkmark wherever it is. Text buttons stay for actions with no standard icon (Filter, Sort).
- **The prominent confirm was added on request** (first planned as a follow-up). The role is the only switch: there is no separate "prominent" flag, since a confirm is the one action a sheet leads with.

## Verification
- **Android emulator (Pixel Tablet):** the edit sheet shows an X on the left and a checkmark on the right, and TalkBack labels are "Cancel" and "Save" (read from the accessibility tree).
- **iOS simulator (iPhone 17 Pro, iOS 26):** the same, as Liquid Glass buttons; the checkmark is prominent (accent-tinted glass, white glyph).
- **Android emulator:** the checkmark is a 40-point accent circle with a white glyph, inside the sheet's edge.
- **Builds:** the Showcase (iOS simulator, Android) and the push sample (Android) build without new warnings.
