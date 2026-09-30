# Issue #275 — Spine.Widgets: build the widget extension for Mac Catalyst

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/275
**Branch:** issue/275-spine-widgets-build-the-widget-extension-for-mac-c
**Status:** Completed

## Plan

Follows the issue's own work list.

1. **Spike the Swift side by hand:** compile the bridge and the extension with `swiftc -target arm64-apple-ios17.0-macabi -sdk macosx`, and see what ActivityKit, AppIntents and WidgetKit's `ActivityConfiguration` allow on Catalyst.
2. **Build script** (`build/spine-widgets-build.sh`): a `maccatalyst` SDK case that
   - builds both architectures and joins them with `lipo`;
   - lays the `.appex` out as a macOS bundle (`Contents/Info.plist`, `Contents/MacOS/<name>`);
   - lays the bridge out as a versioned framework (`Versions/A`, `Current`), which codesign requires on macOS;
   - writes the extension's plist with `CFBundleSupportedPlatforms` MacOSX, `LSMinimumSystemVersion` and `UIDeviceFamily` 2 and 6;
   - gives the extension `com.apple.security.app-sandbox`, and leaves out Live Activities (ActivityKit is iOS only).
3. **Targets** (`build/Plugin.Maui.Spine.Widgets.targets`): accept `maccatalyst` in the conditions, with the SDK and architectures taken from the RuntimeIdentifier(s). Also accept it in the App Group entitlement, and in `Plugin.Maui.Spine.Common`'s shared entitlements step if it is limited to iOS.
4. **Runtime:** compile `Platforms/iOS` for Mac Catalyst too (`WidgetPlatform`, the lifecycle hooks). Activities stay disabled there: `AreActivitiesEnabled` false, `StartAsync` null.
5. **Verify:** the Showcase built for Mac Catalyst shows its widgets in the macOS widget gallery. They render, follow the timeline, and open the app when clicked. Buttons run.
6. **Docs:** the platform table and Mac signing notes in `docs/wiki/widgets.md`, and the `spine-widgets` skill.

## Open Questions


## Changes

- Swift: `ActivityKit` and everything built on it (`SpineActivityAttributes`, the bridge's activity members, `SpineLiveActivity`) is inside `#if !targetEnvironment(macCatalyst)`. On Catalyst the bridge keeps the same `@objc` selectors, answering "no activities", so the C# side calls them unchanged.
- `spine-widgets-build.sh`: a `maccatalyst` SDK case:
  - `-target <arch>-apple-ios<min>-macabi` against the macOS SDK, with its `iOSSupport` frameworks and Swift libraries on the search paths;
  - the `.appex` as a macOS bundle (`Contents/Info.plist`, `Contents/MacOS`, `Contents/Resources` for the manifest and `Metadata.appintents`);
  - the bridge as a versioned framework (`Versions/A`, `Current` and the top-level symlinks) with a matching install name;
  - `LSMinimumSystemVersion` (the iOS version minus 3), `UIDeviceFamily` 2 and 6, and `DT*` keys from the macOS SDK;
  - `com.apple.security.app-sandbox` in the extension's entitlements, and Live Activities off.
- `Plugin.Maui.Spine.Widgets.targets`: `_SpineWidgetsApple` (ios or maccatalyst) replaces the iOS-only conditions; a `maccatalyst-*` RuntimeIdentifier selects the new SDK case.
- C#: `Platforms/iOS/*.cs` became `Extensions/SpineWidgetsExtensions.Apple.cs` and `Services/WidgetPlatform.Apple.cs` inside `#if IOS || MACCATALYST`, the convention the core uses for `*.Apple.cs`, so Mac Catalyst runs the iOS `WidgetPlatform`. `SpineWidgetsExtensions.Default.cs` excludes `MACCATALYST`.
- Targets: two build warnings for Mac Catalyst, one for a `group.…` App Group without `CodesignProvision` and one for an ad hoc signed build, since either leaves the widget empty.
- Targets: universal (Release) Catalyst builds.
  - `_SpineWidgetsAlignUniversalBundles` copies the first architecture's `Metadata.appintents` (the app's and the extension's) and the extension's `_CodeSignature` into the other bundles before `_CreateMergedAppBundle`.
  - `_SpineWidgetsSignMergedExtension` signs the merged `.appex` with the app's identity and the extension's entitlements after it.
- Sample:
  - `SpineWidgetsAppGroup` is `7F2CQZ9T84.se.cosmomedia.spineshowcase` for maccatalyst, and the Mac Catalyst entitlements (the app is sandboxed) declare the same group.
  - A personal `CodesignKey` goes in the git-ignored `MauiSpineSampleApp.csproj.user`.
- Docs: `docs/wiki/widgets.md` (platform table, a "Mac Catalyst" setup section, build requirements, limits), the `spine-widgets` and `spine-setup` skills.

### Spike results (2026-09-29)

- Both Swift modules compile for `arm64-apple-ios17.0-macabi` once `iOSSupport` is on the paths; WidgetKit, SwiftUI, AppIntents and `LiveActivityIntent` are all available.
- The sample's Catalyst Debug build contains `Contents/PlugIns/SpineWidgets.appex` (sandbox and App Group entitlements) and a versioned `Contents/Frameworks/SpineWidgetBridge.framework`. `codesign --verify --deep --strict` passes on the ad-hoc signed app.
- After `open`, PlugInKit lists `se.cosmomedia.spineshowcase.SpineWidgets` (`pluginkit -m -A -D`), and LaunchServices records it as a Mac Catalyst plug-in.
- The iOS simulator build is unchanged: flat `.appex`, same targets.
- First run on the Mac: the widget showed "—" and clicking Widgets in the app did nothing. `IWidgetPlatform` was not registered, because MAUI's single-project convention removes everything under `Platforms/` except the current platform's folder, so `Platforms/Apple` compiled for no platform, iOS included. Moving the files out fixed both.
- With `group.se.cosmomedia.spineshowcase` and an ad hoc signed build, macOS 15 said "Data Access Blocked" and the widget stayed empty. With `7F2CQZ9T84.se.cosmomedia.spineshowcase` and the Apple Development certificate, no warning appeared and the widget showed its content (Jonatan, on his Mac).
- Release (universal) build: the extension and the bridge are `x86_64 arm64`, `codesign --verify --deep --strict` passes, and the extension is signed by team 7F2CQZ9T84.
- iOS simulator (iPhone 17 Pro) after the move: `IWidgetService.IsSupported` is true and the Widgets page opens.
- Not verified: clicking the widget to open the app and the Bump button on the Mac. A second "Data Access Blocked" came after the switch to the Team ID group, and its cause is not known yet.

## Decisions

- **The App Group on the Mac is set explicitly (Jonatan's choice).** A Mac Catalyst app sets `SpineWidgetsAppGroup` to `<TeamID>.<name>` and signs with its certificate, and the build warns when it does not. The alternatives were deriving the Team ID from the signing identity (entitlements would have to be written after the SDK picks the identity), or keeping `group.…` and requiring profiles for the app and the extension.
- **No Live Activity on the Mac.** ActivityKit is not in the Mac Catalyst SDK. The bridge keeps the same selectors and answers "none", so there is one C# implementation for both platforms.
- **The `.Apple.cs` files live outside `Platforms/`.** MAUI's single-project convention compiles nothing under `Platforms/` except the current platform's folder.
