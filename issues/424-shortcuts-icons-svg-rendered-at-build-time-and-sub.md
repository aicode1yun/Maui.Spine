# Issue #424 — Shortcuts: icons (SVG rendered at build time) and subtitles

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/424
**Branch:** issue/424-shortcuts-icons-svg-rendered-at-build-time-and-sub
**Status:** Completed

## Plan

### What MAUI does with an icon (read from Microsoft.Maui.Essentials 10.0.50)

- `RegisterShortcuts` (`src/Plugin.Maui.Spine/Extensions/MauiAppBuilderExtensions.cs`) builds `new AppAction(id, title)`: no subtitle and no icon reach MAUI.
- iOS `AppActionsExtensions.ToShortcutItem`: `UIApplicationShortcutIcon.FromTemplateImageName(action.Icon)`, with the name as given. SpringBoard draws it, so the image must be in the app bundle.
- Android `ToShortcutInfo`: `Path.GetFileNameWithoutExtension(icon)`, then `Resources.GetIdentifier(name, "drawable")` and `Icon.CreateWithResource`. An unknown name gives id 0.
- A `MauiImage` with `Link` metadata is written under the Link's name, so Resizetizer can render a Spine icon SVG from a package folder to `spine_shortcut_qrcode.png` (iOS @1x/@2x/@3x) and a drawable of the same name (Android).

### 1. Spike: does SpringBoard take a MAUI-rendered PNG as a template icon?

- `SpineShortcut` gets `Icon` and `Subtitle`; `IShortcutBuilder.Add(id, title, showInTray, icon, subtitle)`; `RegisterShortcuts` passes both on to `AppAction`.
- The sample adds a `MauiImage` by hand, with `Link="spine_shortcut_theme.svg"`, for `Theme.svg` from `Plugin.Maui.Spine.Svg.Icons/Images`, and gives the "theme" shortcut that icon.
- Check the long-press menu on the iOS simulator (icon tinted like the system's own) and on the Android emulator.

### 2. After the spike: the build side

- `<SpineShortcutIcon Include="qrcode.svg" />` in `src/Plugin.Maui.Spine/build/Plugin.Maui.Spine.targets`, resolved against the app's own SVGs (`EmbeddedResource`/`MauiImage` paths) and `Plugin.Maui.Spine.Svg.Icons/Images`, case-insensitive like `SvgImageSource`. Each becomes a `MauiImage` with `Link="spine_shortcut_<name>.svg"`.
- `Add(..., icon: "qrcode.svg")` maps the name to `spine_shortcut_qrcode` before it goes to `AppAction`.
- An icon named in `Add` without a `SpineShortcutIcon`: iOS falls back to the SF Symbol of that name (`W.Icon` convention); Android leaves the icon out rather than hand MAUI a resource id of 0. Either way a warning is logged.
- Mac and Windows tray menu items: the SVG rendered at run time, the way the tray icon already is.
- Docs: `docs/wiki/shortcuts.md` (icons, subtitles, and iOS is supported: MAUI registers Home Screen quick actions), the `/spine-setup` skill if it mentions shortcuts.

## Open Questions


## Changes

- Spike: `SpineShortcut` gains `Icon` and `Subtitle`, `IShortcutBuilder.Add` takes `icon` and `subtitle`, and `RegisterShortcuts` passes both to `AppAction`. The sample adds `Theme.svg` from the icon set as a `MauiImage` with `Link="spine_shortcut_theme.svg"`, and the "theme" shortcut names it.

### Spike results (2026-09-29)

- **iOS 26.4 simulator (iPhone 17 Pro):** Resizetizer writes `spine_shortcut_theme.png`, `@2x` and `@3x` loose into the bundle. The long-press menu shows the icon tinted like the system's own, with the subtitle under the title. Tapping it opens the Theming page.
- **Android emulator (Pixel 10 Pro, Android 17):** the APK carries `drawable-*/spine_shortcut_theme.png`. `dumpsys shortcut` lists `iconRes=drawable/spine_shortcut_theme` and `longLabel` = the subtitle. The launcher's long-press menu shows the icon, and tapping it opens the Theming page. The Pixel launcher shows the short label only.

### Implementation

- `SpineShortcut(Id, Title, ShowInTray, Icon, Subtitle)` and `IShortcutBuilder.Add(..., icon, subtitle)`; both optional, so existing calls compile unchanged.
- `Core/ShortcutIcons.Names.cs`: the name mapping (`Key`, `ImageName` = `spine_shortcut_<key>`, `SvgFile`, `SystemName`), free of MAUI and compiled into `tests/Plugin.Maui.Spine.Core.Tests` (`ShortcutIconsTests`).
- `Core/ShortcutIcons.cs`: `ForAppAction` hands MAUI the image name only when the app carries it (Android: `GetIdentifier` > 0; iOS: `UIImage.FromBundle`); `Initializer` (an `IMauiInitializeService` registered after MAUI's) gives undeclared icons the SF Symbol of the same name on iOS and logs a warning for each; `ForTray` loads the embedded SVG for the tray menus and logs when it is missing.
- `RegisterShortcuts` passes the subtitle and `ForAppAction`'s icon to `AppAction` and registers the initializer.
- Mac tray menu items get a 16 pt template `NSImage` from the SVG's PDF (`AppKitObjC.Void_msgSend_CGSize` added for `setSize:`); Windows tray items get a monochrome `BitmapIcon` from `SvgIcon.GetPngFilePath(32)`.
- `build/Plugin.Maui.Spine.targets`: `_SpineAddShortcutIcons` (before `ResizetizeCollectItems`) matches each `SpineShortcutIcon` against the app's SVG `EmbeddedResource`/`MauiImage` items, then `SpineShortcutIconSource`, adds the first match per key as `MauiImage Link="spine_shortcut_<key>.svg" BaseSize="35,35"`, and warns for a name with no SVG.
- `Plugin.Maui.Spine.Svg.Icons`: new `build/Plugin.Maui.Spine.Svg.Icons.props` declares its SVGs as `SpineShortcutIconSource`; the package now carries them under `build/Images` and `buildTransitive/Images` (checked with `dotnet pack`).
- Sample: `<SpineShortcutIcon Include="theme" />`, and the "theme" shortcut has `icon: "theme"` and a subtitle.
- Docs: `docs/wiki/shortcuts.md` (icons and subtitles section, parameters, iOS quick actions in the platform table), `README.md`, `src/Plugin.Maui.Spine/README.md`.

### Verified (2026-09-29)

- **iOS (iPhone 17 Pro simulator, iOS 26.4)** with a temporary set of shortcuts: `theme` and `QrCode.svg` from the set show their SVGs with the subtitle; an undeclared `star.fill` shows the SF Symbol; an undeclared `no.such.icon` gets iOS's default dot. The build warned for `<SpineShortcutIcon Include="no.such.icon" />`.
- **Android (Pixel 10 Pro emulator, Android 17):** `dumpsys shortcut` lists `drawable/spine_shortcut_theme` and `drawable/spine_shortcut_qrcode`, and no icon (not resource id 0) for the undeclared ones.
- **Mac Catalyst:** the sample starts and runs with the tray menu built. The menu itself could not be captured.
- **Windows:** not built; `makepri.exe` cannot run on macOS, and the Windows target is only in the build on Windows.

## Decisions

- **Build time, through Resizetizer, on both platforms.** SpringBoard only reads images from the bundle, so iOS needs the build step anyway. The same `MauiImage` gives Android its drawable, so Android needs no path of its own. MAUI's `AppAction` keeps handling registration and taps.
- **Only declared icons are bundled.** `Configure` runs at run time, so the build cannot read the names it uses; bundling all 220 icons at three scales would be too heavy.
- **The SF Symbol fallback patches MAUI's items after the fact.** MAUI's `AppAction` knows only template image names. Its iOS `SetAsync` sets `ShortcutItems` synchronously inside its own initializer, so an initializer registered later copies each affected item with only the icon changed, keeping MAUI's type and `UserInfo`, which its tap handling reads.
- **Android gets no icon rather than id 0.** MAUI calls `Icon.CreateWithResource` with whatever `GetIdentifier` returns; Spine checks first and passes `null`.
- **The tray menus use the SVG at run time.** They are the app's own menus, so no build step is needed there; a missing SVG is logged, not thrown.
