# Issue #411 — Header bar actions: animated transitions that feel native

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/411
**Branch:** issue/411-header-bar-actions-animated-transitions-that-feel
**Status:** In Progress

## Plan

Path 1 of the issue, in Spine's own header bar on every platform: overlapping swaps, a small scale on show and hide, per-platform timing, Reduce Motion respected.

Path 2, on iOS 26 with glass: one glass button whose content and frame change inside a UIKit spring, so the glass morphs between circle and capsule.

## Open Questions

## Changes

- `PageActionView` draws on two faces. Replacing the action (Back → Cancel, Filter → Bell, a new icon) crosses over: the new face fades in and grows from 0.85 while the old one fades and shrinks out in the first 70 % of the time, together, instead of a 60 ms fade out, a 50 ms pause and a 60 ms fade in. The outgoing face keeps its size, so a glass circle and a text capsule overlap at the slot's edge rather than squeezing each other.
- The view sizes to its content; the icon slot width moved to a new `IconWidth` (set by `HeaderBarView`, and to 46 by the Windows title bar), so a fading face is never clipped by the new action's width.
- Show / hide (`HeaderBarView.AnimateVisibility`): fade plus scale, cancelling a running animation instead of racing it.
- Timings: 300 ms on Apple, 150 ms on Android (Material 3 fade-through), 200 ms on Windows, `CubicOut`; under Reduce Motion a 150 ms fade without scaling.

- Path 2 (`SpineOptions.Apple.MorphHeaderActions`, default on with glass): a single glass `Button` draws icons (the SVG as its image, a 44 pt circle) and text (a capsule). A replacement fades the old content out (90 ms), puts the new content in invisibly, lays the bar out inside a 0.45 s spring (damping 0.82) so UIKit morphs the glass, and fades the new content in. Off: path 1.
- Liquid Glass is never faded through its alpha, which draws a flat grey stand-in (what showed dark grey on the device): `GlassAppearance` materializes and dissolves the glass effect and fades only the title and image. Used for show, hide, path 1's swap and path 2's content.
- An action on an arriving page waits one frame for UIKit to build its glass before it materializes, instead of showing grey for its first frames; both actions of a page start together (an earlier version waited up to 300 ms and pre-faded the morphing action for 90 ms, so the right action came, then the left).
- The morph starts at once: new content in invisibly, the glass springs to its size, the content fades in from 0.1 s.
- A centred title keeps its natural width in the middle of the bar (`TitleSlotLayout`), truncated only when it does not fit. It used to fill the room between the actions and fall back to the left when it did not fit, so it jumped when Back became Cancel, and animating its frame inside the spring made UIKit redraw it off centre.
- Glass text capsules drop the plain text button's 12 pt inset, so they line up with the page margin like the icon circles.

- Removal is quicker than arrival (`RemovalDuration`, half of `TransitionDuration`), and the whole bar fades without scaling when a page hides it.
- Going back, the header switches to the returning page at the start of the transition (`NavigationRegionViewModel.HeaderRegionViewModel`), as a navigation bar does; the leaving page's actions used to stay about 250 ms over the page underneath, until the transition had finished.
- The morph flushes any pending layout without animation first, then runs the window's layout pass inside the spring, so only this action's change is animated. (Laying out only the header bar left each button at its previous size on the device: MAUI measures in the window's pass.)
- The morphing button never holds an image and never gets an empty title: the icon is an `Image` drawn over the glass circle, and icon mode keeps a zero-width title. A glass configuration whose title went empty drew the next title (Cancel, the second time) in the label colour instead of the accent.
- A centred title that has not been measured yet keeps the room between the actions instead of a zero-width frame it would grow out of.

Verified in the iOS 26.4 simulator from screen recordings: Back ↔ Cancel crosses over without a gap, Filter hidden lets Bell take its place, and it comes back. Android built only.

## Decisions

- Path 1's glass shape does not morph (each face has its own glass); path 2 does, and is the default with glass. Path 1 stays for other platforms, glass off, and `MorphHeaderActions = false`.
- `GlassAppearance` finds the glass (`UIVisualEffectView`) and the content (`UILabel`, `UIImageView` outside it) in the button's native hierarchy: UIButton's glass configuration exposes neither.
- Stacking by `ZIndex` rather than re-adding the incoming face to the grid, which would re-create its native view and its glass.
- `HeaderBarConstants.FadeInDuration` / `FadeOutDuration` are no longer used by the bar but stay: they are public.
