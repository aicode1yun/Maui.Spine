using Android.Content;

namespace Plugin.Maui.Spine.Widgets.Services;

/// <summary>
/// Work a receiver hands off with <c>GoAsync()</c>. Android still holds the broadcast to its time limit —
/// 60 seconds for a background broadcast, after which the app is reported as not responding and the
/// process can be killed — so the work gets a token cancelled after <see cref="Budget"/>, and the
/// broadcast is finished at <see cref="Deadline"/> whether or not the work honoured it.
/// </summary>
internal static class ReceiverWork
{
    internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(20);
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(25);

    /// <param name="outOfTime">Called with <c>true</c> when the work stopped on its token, <c>false</c> when it
    /// was still running at the deadline and the broadcast was finished without it.</param>
    internal static void Run(BroadcastReceiver.PendingResult? pending, Func<CancellationToken, Task> work, Action<Exception> failed, Action<bool> outOfTime)
    {
        Task.Run(async () =>
        {
            // Not disposed: work that overran still holds the token after the broadcast is finished.
            var cancellation = new CancellationTokenSource(Budget);
            Task? running = null;
            try
            {
                running = work(cancellation.Token);
                await running.WaitAsync(Deadline);
            }
            catch (TimeoutException) when (running is { IsCompleted: false }) { outOfTime(false); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { outOfTime(true); }
            catch (Exception e) { failed(e); }
            finally { pending?.Finish(); }
        });
    }
}
