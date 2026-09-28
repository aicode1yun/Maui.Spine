# Issue #409 — Header bar buttons and page content share the platform's side margin

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/409
**Branch:** issue/409-header-bar-buttons-and-page-content-share-the-pla
**Status:** Completed

## Plan

- `HeaderBarConstants.PageMargin` (UIKit's system layout margin on Apple: 16 pt below 414 pt wide, 20 pt on Plus / Pro Max / iPad; 16 on Android and Windows) and `PagePadding` for XAML.
- Glass header buttons (iOS 26+) as 44 pt circles with their outer edge at `PageMargin`, in regions and sheets; the large title at the same margin.
- The Showcase aligns its content with `PagePadding`.

## Open Questions

## Changes

- `HeaderBarConstants.PageMargin`, `PagePadding`; `LargeTitleMargin` follows `PageMargin`.
- `HeaderBarView`: with glass header actions the side column is `PageMargin` and icon slots are 44 pt (was 8 + 48 on region pages, which put the back button at 9.6 pt).
- `SpineSection` header on iOS 26: bold, 17 pt, as in Settings ("Mina enheter"), measured on the iPhone.
- Showcase: `SampleLayout`, header-in-list pages, sheets and the main page's title and theme button use `PageMargin`.
- Showcase: a `Choice` selects itself in its `ChoiceGroup` when tapped; Value placement on the Rows options (and any group whose action did not reselect) looked stuck on its first chip.

Verified on the iOS 26.4 simulator: back button and content both at 16 pt; Value placement chips switch.

## Decisions

- Content is not padded automatically: full-bleed pages (photos, maps, lists) must stay edge to edge, so pages opt in with `PagePadding`.
- Without glass (before iOS 26, or `GlassHeaderActions = false`) the old slots stay: the plain glyph buttons were placed for the older navigation bar.
