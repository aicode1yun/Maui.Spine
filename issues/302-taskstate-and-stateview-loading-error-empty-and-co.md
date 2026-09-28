# Issue #302 — TaskState and StateView: loading, error, empty and content from one async source

**GitHub:** https://github.com/jonatansoderberg/Maui.Spine/issues/302
**Branch:** issue/302-taskstate-and-stateview-loading-error-empty-and-co
**Status:** Completed

## Plan

`TaskState<T>` wraps an async load and reports its state. `ViewModelBase.Load(...)` ties it to the page's lifetime. `StateView` shows the page's content, a spinner, an error with a retry button, or an empty message, depending on that state. Both live in the core package (`Plugin.Maui.Spine`), with no native code and no dependency on Shimmer. The skeleton is reached through a `bool` the page binds to, as the design note from #336 on the issue lays out.

### `TaskState` / `TaskState<T>` (`src/Plugin.Maui.Spine/Core/TaskState.cs`)
- `TaskStatus` enum (named `TaskStateStatus` to avoid clashing with `System.Threading.Tasks.TaskStatus`): `NotStarted`, `Loading`, `Refreshing`, `Success`, `Empty`, `Error`.
- Non-generic abstract base `TaskState : ObservableObject`, so that `StateView.State` can bind to any `TaskState<T>`:
  - `Status`, `Error` (`Exception?`), `ErrorMessage` (`string?`, the exception's message, so the default template shows the real cause and not only "something went wrong").
  - `IsLoading` (only `Loading`, not `Refreshing`), `IsRefreshing` (for `RefreshView.IsRefreshing`, two-way), `HasResult`.
  - `object? Value` for the view.
  - `LoadCommand`, `RefreshCommand`, `RetryCommand` (`IAsyncRelayCommand`); Retry equals Load.
  - `Task LoadAsync(CancellationToken)`, `Task RefreshAsync(CancellationToken)`.
- `TaskState<T>`:
  - Constructor `(Func<CancellationToken, Task<T>> load, Func<T, bool>? isEmpty = null, Func<T>? placeholder = null)`.
  - `T? Result`: the last successful value, kept while `Refreshing` and after a failed refresh.
  - `new T? Value`: `Result` when there is one; otherwise the placeholder while `Loading`. This is what gives a skeleton list the height it will have once loaded.
- Semantics:
  - A new load cancels the one in flight, so the last one wins.
  - Load when a `Result` exists → `Refreshing`, otherwise → `Loading`.
  - `OperationCanceledException` from the page's cancellation → back to the state before (`NotStarted` or the previous `Success`/`Empty`), not `Error`.
  - Any other exception → `Error`, logged with `Debug.WriteLine` like `Poll`. A failed *refresh* keeps `Result` on screen and sets `Error`. The status goes to `Error` only when there is nothing to show.
  - Awaited on the caller's context (the UI thread from the lifecycle), so bound properties are set there, just as `Poll` does it.

### Lifetime (`ViewModelBase`)
- `protected TaskState<T> Load<T>(Func<CancellationToken, Task<T>> load, Func<T, bool>? isEmpty = null, Func<T>? placeholder = null)` creates and registers the state. Called from the constructor.
- On appearing (in `BeginLifetime`): a registered state that is `NotStarted`, or whose last load was cancelled or failed, loads with `PageLifetime`. A state with a `Result` is not reloaded when you navigate back to the page. The app decides that with `OnAppearingAsync` or `OnResumedAsync` → `State.RefreshAsync()`.
- On disappearing (`EndLifetime`): the `PageLifetime` token cancels the load in flight. Nothing else is needed.
- `LoadAsync`/`RefreshAsync` without a token on a registered state use `PageLifetime`, so `RetryCommand` and `RefreshCommand` also stop when the page disappears.
- `new TaskState<T>(...)` without `Load` still works for a view model that is not a page (explicit `LoadAsync(ct)`).

### `StateView` (`src/Plugin.Maui.Spine/Presentation/StateView.cs`)
- `ContentView` with `State` (`TaskState`), `LoadingTemplate`, `ErrorTemplate`, `EmptyTemplate` (`DataTemplate?`), and a read-only `IsLoading`.
- Content is the ordinary `Content`. It inherits the page's `BindingContext` and binds to `Competitions.Value` (the user's choice; see Decisions).
- What is shown:
  - `NotStarted`, `Loading` without a placeholder → loading template.
  - `Loading` with a placeholder (`Value` not null) → the content, so the skeleton can draw it. The same view stays through `Success`, so nothing is rebuilt and nothing jumps.
  - `Refreshing`, `Success` → the content.
  - `Empty` → empty template. `Error` → error template.
- The content view is created once and hidden or shown (`IsVisible`), never rebuilt. State templates are created lazily, once each. They sit in a `Grid` alongside the content.
- The templates' `BindingContext` is the `TaskState`, so the error template binds to `ErrorMessage` and `RetryCommand` directly.
- Defaults when no template is set, resolved as: instance → application resource `DefaultStateViewLoadingTemplate` / `DefaultStateViewErrorTemplate` / `DefaultStateViewEmptyTemplate` → code default. This is the same `Default…` key convention as `SpineStyleOptions`, but for templates, without a style-options class.
  - Loading: a centred `ActivityIndicator` in the theme's accent colour.
  - Error: title `State.Error.Title` ("Couldn't load"), `ErrorMessage` in a smaller, dimmed style, button `State.Error.Retry` ("Try again").
  - Empty: `State.Empty.Title` ("Nothing here yet"). `StateView.EmptyText` overrides the text without a full template.
- StateView does **not** set `Skeleton.IsActive`, because the core cannot reference Shimmer. The page writes `Skeleton.IsActive="{Binding Competitions.IsLoading}"` on its content root.

### Strings
- `src/Plugin.Maui.Spine/Resources/Strings/strings.xml` + `strings.sv.xml`: `State.Error.Title`, `State.Error.Retry`, `State.Empty.Title` (in the core package, alongside `Spine.Header.*`).

### Sample (Spine Showcase)
- New page `Pages/State/StatePage` ("Loading states"), with an entry in `SampleIndex` with PackageChips `Plugin.Maui.Spine` and an icon from the set. `GlobalXmlns.cs` + csproj as usual. Examples:
  1. A list with default templates: a fake service with a delay, with switches for "fail" and "empty". The spinner, the error with retry and the empty text are shown in turn.
  2. Skeleton + TaskState: the Shimmer list with `placeholder:` and `Skeleton.IsActive="{Binding People.IsLoading}"`, with no hand-written `OnIsLoadingChanged`.
  3. Pull to refresh: `RefreshView` bound to `IsRefreshing`/`RefreshCommand`. The old list stays visible, and a failed refresh keeps the list.
  4. Custom templates (an error template of your own).
- The Shimmer page's "A list that does not jump" example keeps its hand-written approach but gets a line pointing to TaskState.

### Docs
- New `docs/wiki/loading-states.md`, linked from the relevant pages (`page-pattern.md`, `shimmer.md`).
- The `spine-page` skill (`.claude/skills/spine-page`) gets a short section on `Load(...)` + `StateView`.

### Tests
- New `tests/Plugin.Maui.Spine.Core.Tests` (net10.0, xunit), set up like `Plugin.Maui.Spine.Svg.Tests`. It compiles `Core/TaskState.cs` in with a `Link`, because the core targets only MAUI platforms. `TaskState` therefore uses no MAUI types, only CommunityToolkit.Mvvm. Cases: last load wins, cancellation → the previous state, a failed refresh keeps `Result`, `isEmpty`, `Value` with and without a placeholder, and `IsLoading`/`IsRefreshing`. Added to `Spine.slnx` and the CI test step.

### Verification
- iOS simulator (iPhone 17) and Android emulator: all four sample examples in light and dark mode. Navigate away during loading and back (load restarts), and navigate back after success (no reload).
- Windows and Mac Catalyst via the CI build.

## Open Questions


## Changes

- `Core/TaskState.cs`: `TaskStateStatus`, the non-generic `TaskState` (status, error, `IsLoading`, `IsRefreshing`, `HasResult`, `HasError`, `HasPlaceholder`, `LoadCommand`, `LoadAsync`, `Reset`) and `TaskState<T>` (`Result`, `Value`, the load itself). The last load wins; a cancellation puts the status back; a failed refresh keeps the result and sets `Error`. No MAUI types.
- `ViewModelBase.Load(load, isEmpty, placeholder)`: registers the state with the page. `BeginLifetime` loads every state with nothing to show (`NotStarted` or `Error`), and `PageLifetime` is the default token, so `EndLifetime` cancels a running load.
- `Presentation/StateView.cs`: a `ContentView` with its own `ControlTemplate` (a `ContentPresenter` and a host for the state view). Default templates for loading (a spinner in the accent), error (title, message, Try again) and empty (a line of text, `EmptyText`). The resources `DefaultStateView{Loading,Error,Empty}Template` override them app-wide. It listens to the state between `Loaded` and `Unloaded`.
- Strings `Spine.State.Loading`, `Spine.State.Error.Title`, `Spine.State.Error.Retry`, `Spine.State.Empty.Title` in English and Swedish.
- `tests/Plugin.Maui.Spine.Core.Tests` (17 tests on `TaskState`), added to `Spine.slnx`, `Spine.Packages.slnf` and the CI test steps.
- Showcase: the new page **Loading states** (`Pages/State`) with three examples: default templates with switches for a 503 and an empty result, a skeleton from a placeholder with a refresh that can fail, and templates of your own for a service that fails every other time. The Shimmer page's list example points to it.
- Docs: the new `docs/wiki/loading-states.md` with two screenshots, links from `README.md`, `getting-started.md`, `page-pattern.md` and `regions.md` (a `Load` row in the lifetime table), and a rewritten "With TaskState" section in `shimmer.md`. Updated skills: the `spine-page` skill (a "Loading data" section, description) and `spine-controls` (skeleton with `TaskState`). `agent-skills.md` also updated.
- Verified on the iPhone 17 Pro simulator (iOS 26.4, light and dark) and on the Pixel 10 Pro emulator: spinner, content, error with the message and retry, empty text, reset on a switch, a skeleton that turns into the list without a jump, a refresh with the list kept and a failed refresh with its message, a custom loading and error template. Not run: a `RefreshView` bound to `IsRefreshing`/`LoadCommand` (documented, not in the sample, which is one scroll view), and a page that reappears after a cancelled load (covered by the tests of `NeedsLoad` and the cancellation, not driven in the app).

## Decisions

- **Lifetime via `ViewModelBase.Load(...)`** (Jonatan, 2026-09-28): the page does not need to call anything in `OnAppearingAsync`. The first appearance loads, and a load that was cancelled or failed runs again when the page reappears.
- **Content as ordinary `Content` with the page's `BindingContext`** (Jonatan, 2026-09-28), not a `ContentTemplate` bound to `Value`. Page commands are reachable without `RelativeSource`, and the same view stays from the skeleton to the loaded state.
- **Core package, not a Controls package:** the issue and the #336 note want a default without Shimmer, and the load belongs with `ViewModelBase`. No native code.
- **`TaskStateStatus`**, not `TaskStatus`: `System.Threading.Tasks.TaskStatus` is in every file through implicit usings.
- **One `LoadCommand`, no separate `RetryCommand`/`RefreshCommand`/`RefreshAsync`.** A load with a result on screen is already a refresh, and one without is already a retry, so the three would be one command under three names. The command allows concurrent execution, because a `RefreshView` disables itself while its command's `CanExecute` is false, and the state's own last-wins replaces the running load anyway.
- **No `object? Value` on the base class.** A `new T? Value` hiding an `object? Value` makes `GetProperty("Value")` ambiguous, which MAUI's binding path resolution does not like, and `StateView` only needs `HasPlaceholder`. `Value` is on `TaskState<T>` only.
- **No `IsLoading` on `StateView`.** The design note proposed one for the skeleton binding. With the content keeping the page's binding context, `{Binding People.IsLoading}` is shorter than a `RelativeSource` to the view.
- **`IsLoading` includes `NotStarted`.** Between the page being bound and appearing there is nothing to show and a load about to start. A skeleton that turned on only once the load had started would flash the empty content first.
- **A failed refresh keeps `Success`/`Empty`** and sets `Error`/`HasError`. `StateView` does not show it, because replacing a list the user is reading with an error for a failed refresh is worse than the stale list. The sample shows it in a line above the list.
- **`Reset()` added** (not in the plan): when what is loaded changes (a filter, a search term), the old result must not stay on screen as "refreshing". The sample's switches use it.
- **Sample delay for the skeleton is 4 s**, long enough to scroll down to the example and see it.

