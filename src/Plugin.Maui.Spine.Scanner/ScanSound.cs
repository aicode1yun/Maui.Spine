namespace Plugin.Maui.Spine.Scanner;

/// <summary>
/// The short sound a scanner plays on a hit, from the platform's own sounds, so the package ships no audio:
/// the "Tink" system sound on iOS and Mac Catalyst, which the silent switch mutes, and the acknowledge tone on
/// Android, at the notification volume.
/// </summary>
internal static class ScanSound
{
#if IOS || MACCATALYST
    private const uint Tink = 1057;
#endif

    public static void Play()
    {
        try
        {
#if IOS || MACCATALYST
            new AudioToolbox.SystemSound(Tink).PlaySystemSound();
#elif ANDROID
            var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 70);
            tone.StartTone(Android.Media.Tone.PropAck, 150);
            // Released after the tone, or the generator holds an audio track until it is collected
            _ = Task.Delay(400).ContinueWith(_ => tone.Release());
#endif
        }
        catch
        {
            // A sound is a nicety; a device without one still returns the code
        }
    }
}
