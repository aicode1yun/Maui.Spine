# Issue #411 — Header bar actions: animated transitions that feel native

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/411
**Branch:** issue/411-header-bar-actions-animated-transitions-that-feel
**Status:** In Progress

## Plan

Path 1 of the issue, in Spine's own header bar on every platform: overlapping swaps, a small scale on show and hide, per-platform timing, Reduce Motion respected. The native glass morph (UIKit animating the glass view's frame) is left for later.

## Open Questions

## Changes

- `PageActionView` draws on two faces. Replacing the action (Back → Cancel, Filter → Bell, a new icon) crosses over: the new face fades in and grows from 0.85 while the old one fades and shrinks out in the first 70 % of the time, together, instead of a 60 ms fade out, a 50 ms pause and a 60 ms fade in. The outgoing face keeps its size, so a glass circle and a text capsule overlap at the slot's edge rather than squeezing each other.
- The view sizes to its content; the icon slot width moved to a new `IconWidth` (set by `HeaderBarView`, and to 46 by the Windows title bar), so a fading face is never clipped by the new action's width.
- Show / hide (`HeaderBarView.AnimateVisibility`): fade plus scale, cancelling a running animation instead of racing it.
- Timings: 300 ms on Apple, 150 ms on Android (Material 3 fade-through), 200 ms on Windows, `CubicOut`; under Reduce Motion a 150 ms fade without scaling.

Verified in the iOS 26.4 simulator from screen recordings: Back ↔ Cancel crosses over without a gap, Filter hidden lets Bell take its place, and it comes back. Android built only.

## Decisions

- The glass shape itself does not morph from circle to capsule: each face has its own glass, so the two crossfade. A real morph needs UIKit to animate one glass view's frame; that is the later step.
- Stacking by `ZIndex` rather than re-adding the incoming face to the grid, which would re-create its native view and its glass.
- `HeaderBarConstants.FadeInDuration` / `FadeOutDuration` are no longer used by the bar but stay: they are public.
