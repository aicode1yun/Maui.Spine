# Issue #435 — iOS: Push and Widgets each write UIBackgroundModes; the app ends up with only "fetch"

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/435
**Branch:** issue/435-ios-push-and-widgets-each-write-uibackgroundmodes
**Status:** Completed

## Plan

The SDK's `CompileAppManifest` merges each `PartialAppManifest` over the app's `Info.plist` with
`MergePartialPlistDictionary`, which merges dictionaries but replaces arrays. Push's `PushManifest.plist`
(`remote-notification`) and Widgets' `HostManifest.plist` (`fetch`) both carry `UIBackgroundModes`, so
the last one wins, and either one also replaces any modes the app declares in its own `Info.plist`.

Fix with the pattern `<SpineEntitlement>` already uses: packages contribute items, one target writes
one partial plist.

1. **`Plugin.Maui.Spine.Common.targets`** — new `<SpineBackgroundMode Include="…" />` item and a
   `_SpineWriteBackgroundModes` target:
   - Runs for ios/maccatalyst, not design-time, when `@(SpineBackgroundMode)` is non-empty.
   - `DependsOnTargets="_DetectAppManifest"` so `$(AppBundleManifest)` is known; reads the app's own
     `UIBackgroundModes` from it with `XmlPeek` (no Mac-only tool, so it also works with the SDK's
     remote-build setups), when the file exists.
   - Writes `obj/…/spine/BackgroundModes.plist` with the de-duplicated union (app modes first, then
     Spine's) and adds it as a `PartialAppManifest`.
   - Hooked `BeforeTargets="CollectAppManifests;_CompileAppManifestInputs;_CompileAppManifest"`, the
     SDK's public collection point.
   - Header comment explains why (array replacement) as the entitlements one does.
2. **`Plugin.Maui.Spine.PushNotifications.targets`** — contribute
   `<SpineBackgroundMode Include="remote-notification" />` under `_SpinePushNotificationsApple`; drop the
   `UIBackgroundModes` lines from `PushManifest.plist` (it keeps `SpinePushNotificationsEnvironment`).
3. **`Plugin.Maui.Spine.Widgets.targets`** — contribute `<SpineBackgroundMode Include="fetch" />` when
   widgets are built and `SpineWidgetsBackgroundRefresh` is true (same conditions as today's
   script branch: Apple TFM, enabled, `@(SpineWidget)` present).
4. **`spine-widgets-build.sh`** — stop writing `UIBackgroundModes`; keep
   `BGTaskSchedulerPermittedIdentifiers`.
5. Docs: `docs/wiki/widgets.md` wording still holds; check the push wiki page and the background-tasks
   proposal (§ finding 1) and note the fix.

### Verify

- Push sample (both packages), simulator build: `plutil -p` shows `remote-notification` and `fetch`.
- `-p:SpineWidgetsBackgroundRefresh=false`: only `remote-notification`.
- Showcase (Widgets, no Push): only `fetch`.
- Temporarily add `audio` to the push sample's `Info.plist`: all three survive.
- Mac Catalyst build of the Showcase still builds.
- Silent push on a device with the app in the background — needs a device run; the simulator does not
  deliver `content-available` pushes, and the push sample's device profile is stale (#250).

## Open Questions

## Changes

- `Plugin.Maui.Spine.Common.targets`: `<SpineBackgroundMode>` items and `_SpineWriteBackgroundModes`, which
  reads the app's own modes from `$(AppBundleManifest)` with `XmlPeek` and writes the union to
  `obj/…/spine/BackgroundModes.plist` as a `PartialAppManifest`.
- Push contributes `remote-notification`; `PushManifest.plist` keeps only `SpinePushNotificationsEnvironment`.
- Widgets contributes `fetch` (enabled, background refresh on, at least one `<SpineWidget>`);
  `spine-widgets-build.sh` no longer writes `UIBackgroundModes` into `HostManifest.plist`.
- Docs: push and widgets wiki pages say the modes sit beside the app's own; the background-tasks
  proposal notes the fix.

### Verified (simulator builds, `plutil -extract UIBackgroundModes`)

| Build | Result |
|---|---|
| Push sample, iOS simulator | `["remote-notification","fetch"]` (was `["fetch"]`) |
| Push sample, `SpineWidgetsBackgroundRefresh=false` | `["remote-notification"]` |
| Push sample with `audio`, `remote-notification` in its own `Info.plist` | `["audio","remote-notification","fetch"]` |
| Showcase (Widgets only), iOS simulator | `["fetch"]` |
| Showcase, Mac Catalyst | `["fetch"]` |

Not verified: a silent push waking the backgrounded app. The simulator does not deliver
`content-available` pushes, and the push sample's device profile is stale (#250).

## Decisions

- Only `UIBackgroundModes` gets the shared treatment. `BGTaskSchedulerPermittedIdentifiers` is also an
  array but only Widgets writes it today; #308 can extend the pattern when a second writer appears.
- The writer is hooked before the SDK's public `CollectAppManifests`; the packages' contributions are
  evaluation-time items, so no package target has to depend on it and no ordering between
  `BeforeTargets` matters.
- `XmlPeek` rather than `plutil`/`PlistBuddy`, so reading the app's plist does not need a Mac tool. A
  binary `Info.plist` in the project would fail the read; MAUI templates and Xcode write XML.
- Widgets' `fetch` stays tied to actually building widgets (`@(SpineWidget)` non-empty), as today, so an
  app that references the package without declaring a widget does not gain a background mode.
