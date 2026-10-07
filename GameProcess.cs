using System.Diagnostics;

namespace LotroArchiver;

public static class GameProcess
{
    public static bool IsRunning()
    {
        var processes = Process.GetProcesses();
        try
        {
            foreach (var p in processes)
            {
                try
                {
                    if (p.ProcessName.StartsWith("lotroclient", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (InvalidOperationException) { /* process exited while we were looking */ }
                catch (System.ComponentModel.Win32Exception) { /* access denied on some system processes */ }
            }
            return false;
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }
}