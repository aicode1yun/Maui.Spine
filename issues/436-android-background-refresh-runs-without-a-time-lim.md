# Issue #436 — Android: background refresh runs without a time limit inside goAsync()

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/436
**Branch:** issue/436-android-background-refresh-runs-without-a-time-lim
**Status:** Completed

## Plan

Three receivers hand work to `Task.Run` behind `GoAsync()` and call `Finish()` only when the work
returns: the background run (`SpineBackgroundReceiver.OnReceive`), a widget's `Refresh(after)` alarm
(`SpineAppWidget.Refresh`) and a button tap (`SpineAppWidget.Tapped`). None has a deadline, so a handler
that hangs keeps the broadcast open past Android's limit (an ANR for the app, and the process may be
killed), and `IBackgroundRefreshHandler`'s token, documented as "signalled when it runs out", is never
signalled on Android.

1. **One helper for the three receivers**, `Platforms/Android/ReceiverWork.cs` (internal static):
   `Run(BroadcastReceiver.PendingResult? pending, Func<CancellationToken, Task> work, Action<Exception> failed, Action overran)`.
   - A `CancellationTokenSource` cancelled after **20 s**, passed to the work.
   - The work is awaited with `WaitAsync` up to **25 s**; past that `overran` is called (the work keeps
     running, it cannot be stopped) and the broadcast is finished anyway.
   - `Finish()` in a `finally`, as today. An `OperationCanceledException` from the deadline is reported
     as running out of time, not as a failure.
2. **`SpineBackgroundReceiver`**: passes the token to `RunBackgroundRefreshAsync`. The overrun warning
   names the handler type (`IBackgroundRefreshHandler` resolved from DI, or "the widget refresh" when
   there is none).
3. **`SpineAppWidget.Refresh`**: the token goes to `FetchRemoteAsync` / `IWidgetService.RefreshAsync(kind, ct)`.
4. **`SpineAppWidget.Tapped`**: `HandleActionAsync` gets an optional `CancellationToken` passed on to
   `RefreshAsync(kind, ct)`. `IWidgetActionHandler.OnActionAsync` has no token, so for the handler itself
   the hard deadline is the only guard. iOS keeps calling it without a token.
5. **Docs**: `IBackgroundRefreshHandler` XML doc states both budgets (about 30 s on iOS, 20 s on
   Android); `docs/wiki/widgets.md` "Background runs" says the same and what happens past it.

### Verify (Android emulator)

Temporarily register a handler in the Showcase that waits with `Task.Delay(Timeout.Infinite, ct)`, fire
the alarm with `adb shell am broadcast … SpineBackgroundReceiver`, and check logcat: cancelled at ~20 s,
warning logged, no broadcast timeout/ANR. Then one that ignores the token (`Task.Delay(Timeout.Infinite)`):
the overrun warning at ~25 s and the broadcast still finished. Before the fix, the same handler for
comparison. The temporary handler is not committed.

## Open Questions

## Changes

- `Platforms/Android/ReceiverWork.cs`: `Run(pending, work, failed, outOfTime)` — token cancelled at
  20 s, broadcast finished at 25 s whether or not the work stopped; `outOfTime(true)` when it stopped on
  the token, `outOfTime(false)` when it was left behind.
- `SpineBackgroundReceiver`: the background run goes through it with the token; both warnings name the
  handler type from `SpineWidgetsOptions.BackgroundRefreshHandler`.
- `SpineAppWidget.Refresh` and `.Tapped`: through the same helper; the token reaches
  `FetchRemoteAsync` / `RefreshAsync(kind, ct)`.
- `SpineWidgetsExtensions.HandleActionAsync`: optional `CancellationToken`, passed to `RefreshAsync`.
- `IBackgroundRefreshHandler` XML doc and `docs/wiki/widgets.md` (Background runs): the budgets on both
  platforms and what happens on Android past them.

### Verified (Pixel 10 Pro emulator, Showcase with a temporary stalling handler, not committed)

The issue's `adb shell am broadcast` does not reach the receiver: it is `exported="false"`, and the
broadcast is skipped with a permission denial for the shell uid (seen in
`dumpsys activity broadcasts history`). The test used the real alarm instead, with
`BackgroundRefreshInterval` set to one minute and a temporary logcat logger (the Debug logger only
writes with a debugger attached).

| Handler | Before the fix | After |
|---|---|---|
| `Task.Delay(Infinite, ct)` | — | cancelled at 20 s, broadcast finished at +20.08 s, warning *used its 20 s and was cancelled in StallHandler436* |
| `Task.Delay(Infinite)` (ignores the token) | **ANR** 70 s after the alarm (*Reason: Broadcast of Intent … BACKGROUND_REFRESH*), process killed: *bg anr* | broadcast finished at 25 s, warning *still running in StallHandler436 after 25 s …*, no ANR in the following minute |

The `Refresh(after)` and button-tap paths share the helper and were only built, not exercised. The
Widgets project builds for iOS with the `HandleActionAsync` signature change.

## Decisions

- **20 s cancel, 25 s finish.** Android's enforced limit for a background broadcast is 60 s
  (`BROADCAST_BG_TIMEOUT`); the `goAsync()` documentation asks for 10 s. 10 s would be too short for a
  sync handler and stricter than iOS (~30 s); 20/25 keeps a wide margin under the enforced limit and
  close to the iOS budget, so a handler written for one platform fits the other.
- One helper rather than three copies, because the deadline logic (two timeouts, overrun vs. failure)
  is easy to get subtly different in each place.
- The reboot gap mentioned under Related in the issue is out of scope; it belongs to #308's move to
  `JobScheduler`.
