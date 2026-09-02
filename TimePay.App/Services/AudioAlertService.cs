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
                switch (warningMinutes)
                {
                    case 10:
                        // 10-Minute Warning: Gentle dual notification
                        SystemSounds.Asterisk.Play();
                        Console.Beep(800, 150);
                        Thread.Sleep(100);
                        Console.Beep(1000, 200);
                        break;

                    case 5:
                        // 5-Minute Warning: Notice alert
                        SystemSounds.Exclamation.Play();
                        Console.Beep(900, 200);
                        Thread.Sleep(100);
                        Console.Beep(900, 200);
                        break;

                    case 1:
                        // 1-Minute Critical Warning: 3 urgent beeps
                        SystemSounds.Hand.Play();
                        for (int i = 0; i < 3; i++)
                        {
                            Console.Beep(1200, 180);
                            Thread.Sleep(80);
                        }
                        break;

                    default:
                        SystemSounds.Exclamation.Play();
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

    public static void PlayFiveSecondsCountdownSound()
    {
        Task.Run(() =>
        {
            try
            {
                SystemSounds.Hand.Play();
                for (int i = 0; i < 5; i++)
                {
                    Console.Beep(1500, 150);
                    Thread.Sleep(50);
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
                Console.Beep(600, 300);
                Thread.Sleep(100);
                Console.Beep(450, 450);
            }
            catch
            {
                try { SystemSounds.Hand.Play(); } catch { }
            }
        });
    }
}
