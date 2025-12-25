using System;
using System.IO;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Simple file logger for debugging console output when running as executable.
    /// </summary>
    public static class SimpleLogger
    {
        private static readonly string _logPath = "game_console.log";
        private static readonly object _lockObj = new();

        static SimpleLogger()
        {
            // Clear log on startup
            try
            {
                File.WriteAllText(_logPath, $"=== Game Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch { }
        }

        public static void Log(string message)
        {
            lock (_lockObj)
            {
                try
                {
                    string timestamp = $"[{DateTime.Now:HH:mm:ss.fff}] ";
                    string logEntry = timestamp + message + Environment.NewLine;
                    File.AppendAllText(_logPath, logEntry);
                    Console.WriteLine(message); // Also print to console
                }
                catch { }
            }
        }

        public static void LogMemoryStats(MemoryArenaStats stats)
        {
            lock (_lockObj)
            {
                try
                {
                    var lines = new System.Text.StringBuilder();
                    lines.AppendLine($"\n========== MEMORY ARENA STATS (F7) ==========");
                    lines.AppendLine($"Max Capacity: {stats.MaxCapacity / (1024 * 1024)}MB ({stats.MaxCapacity} bytes)");
                    lines.AppendLine($"Total Allocated: {stats.TotalAllocated / (1024 * 1024)}MB ({stats.TotalAllocated} bytes)");
                    lines.AppendLine($"Usage: {stats.UsagePercent:F1}%");
                    lines.AppendLine($"Active Allocations: {stats.ActiveAllocations}");
                    lines.AppendLine($"Deallocations: {stats.TotalDeallocations}");
                    lines.AppendLine($"\n--- Top 10 Allocations ---");
                    
                    foreach (var (name, bytes, count) in stats.TopAllocations)
                    {
                        lines.AppendLine($"  {name}: {bytes / 1024}KB (count: {count})");
                    }
                    
                    lines.AppendLine("============================================\n");
                    
                    File.AppendAllText(_logPath, lines.ToString());
                    Console.WriteLine(lines.ToString());
                }
                catch { }
            }
        }
    }
}
