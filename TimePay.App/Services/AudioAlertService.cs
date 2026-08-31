using System.Media;

namespace TimePay.App.Services;

/// <summary>
/// Service providing distinct audio cues for warning thresholds and session expiration.
/// </summary>
public static class AudioAlertService
{
    public static void PlayWarningSound(int warningMinutes)
    {
        Task.Run(() =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                switch (warningMinutes)
                {
                    case 10:
                        // 10-Minute Warning: Gentle dual chime loop for 5 seconds
                        SystemSounds.Asterisk.Play();
                        while (sw.ElapsedMilliseconds < 5000)
                        {
                            Console.Beep(800, 300);
                            Thread.Sleep(150);
                            if (sw.ElapsedMilliseconds >= 4800) break;
                            Console.Beep(1000, 300);
                            Thread.Sleep(250);
                        }
                        break;

                    case 5:
                        // 5-Minute Warning: Notice alert double-beep loop for 5 seconds
                        SystemSounds.Exclamation.Play();
                        while (sw.ElapsedMilliseconds < 5000)
                        {
                            Console.Beep(900, 250);
                            Thread.Sleep(100);
                            if (sw.ElapsedMilliseconds >= 4800) break;
                            Console.Beep(900, 250);
                            Thread.Sleep(400);
                        }
                        break;

                    case 1:
                        // 1-Minute Critical Warning: Urgent triple-beep alert loop for 5 seconds
                        SystemSounds.Hand.Play();
                        while (sw.ElapsedMilliseconds < 5000)
                        {
                            Console.Beep(1200, 180);
                            Thread.Sleep(70);
                            Console.Beep(1200, 180);
                            Thread.Sleep(70);
                            if (sw.ElapsedMilliseconds >= 4800) break;
                            Console.Beep(1200, 180);
                            Thread.Sleep(300);
                        }
                        break;

                    default:
                        SystemSounds.Exclamation.Play();
                        while (sw.ElapsedMilliseconds < 5000)
                        {
                            Console.Beep(950, 300);
                            Thread.Sleep(200);
                        }
                        break;
                }
            }
            catch
            {
                // Fallback to standard system chime if sound hardware is busy
                try { SystemSounds.Exclamation.Play(); } catch { }
            }
        });
    }

    /// <summary>
    /// Plays an intense 5-second countdown audio alert (5, 4, 3, 2, 1) before the screen locks.
    /// </summary>
    public static void PlayFiveSecondsCountdownSound()
    {
        Task.Run(() =>
        {
            try
            {
                for (int i = 5; i >= 1; i--)
                {
                    if (i == 1)
                    {
                        Console.Beep(1500, 600); // Final urgent tone before lock
                    }
                    else
                    {
                        Console.Beep(1000 + (5 - i) * 120, 250); // Ascending warning beeps
                        Thread.Sleep(750);
                    }
                }
            }
            catch
            {
                try { SystemSounds.Hand.Play(); } catch { }
            }
        });
    }

    public static void PlayExpiredSound()
    {
        Task.Run(() =>
        {
            try
            {
                SystemSounds.Hand.Play();

                // Play continuous alarm beeps for 5 full seconds (5,000 ms)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 5000)
                {
                    Console.Beep(850, 350);
                    Thread.Sleep(80);
                    if (sw.ElapsedMilliseconds >= 4800) break;
                    Console.Beep(600, 350);
                    Thread.Sleep(80);
                }
            }
            catch
            {
                try { SystemSounds.Hand.Play(); } catch { }
            }
        });
    }
}
