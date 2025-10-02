using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace MinecraftSpeedrunTracker
{
    public partial class MainWindow : Window
    {
        private MinecraftSpeedrunAnalyzer analyzer;
        private SpeedrunStatistics currentStats;
        private Logger logger;

        public MainWindow()
        {
            InitializeComponent();
            logger = new Logger();
            InitializeAnalyzer();
            LoadData();
        }

        private void InitializeAnalyzer()
        {
            try
            {
                analyzer = new MinecraftSpeedrunAnalyzer();
                PathText.Text = $"Analyzing: {analyzer.WorldsPath}";
                logger.LogInfo($"Application started. Monitoring path: {analyzer.WorldsPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing analyzer: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                logger.LogError($"Failed to initialize analyzer: {ex.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                logger.LogInfo("Starting world analysis...");
                currentStats = analyzer.AnalyzeWorlds();
                logger.LogInfo($"Analysis complete. Found {currentStats.TotalWorlds} worlds.");

                UpdateUI();
                logger.WriteDetailedReport(currentStats);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                logger.LogError($"Failed to load data: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            // Update stat cards
            TotalWorldsText.Text = currentStats.TotalWorlds.ToString();
            WorldsPlayedText.Text = currentStats.WorldsPlayed.ToString();
            WorldsAbandonedText.Text = currentStats.WorldsAbandoned.ToString();
            ResetRatioText.Text = $"{currentStats.ResetRatio:P1}";

            // Update performance metrics
            AvgPlaytimeText.Text = FormatTimeSpan(currentStats.AveragePlaytimePerWorld);
            AvgFirstActivityText.Text = FormatTimeSpan(currentStats.AverageTimeToFirstActivity);
            TotalStorageText.Text = FormatBytes(currentStats.TotalStorageUsed);
            AvgWorldSizeText.Text = FormatBytes(currentStats.AverageWorldSize);

            // Update session stats
            TodayResetsText.Text = currentStats.TodayResets.ToString();
            WeekResetsText.Text = currentStats.WeekResets.ToString();
            FastestResetText.Text = currentStats.FastestReset.HasValue
                ? FormatTimeSpan(currentStats.FastestReset.Value)
                : "N/A";
            LongestSessionText.Text = currentStats.LongestSession.HasValue
                ? FormatTimeSpan(currentStats.LongestSession.Value)
                : "N/A";

            // Update worlds grid
            var displayWorlds = currentStats.AllWorlds.Select(w => new
            {
                WorldName = w.WorldName,
                CreatedDisplay = w.CreationTime.ToString("MM/dd HH:mm"),
                StatusDisplay = w.WasActuallyPlayed ? "PLAYED" : "RESET",
                PlaytimeDisplay = FormatTimeSpan(w.EstimatedPlaytime),
                SizeDisplay = FormatBytes(w.TotalFileSize)
            }).ToList();

            WorldsDataGrid.ItemsSource = displayWorlds;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            logger.LogInfo("Manual refresh triggered");
            LoadData();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filename = $"speedrun_stats_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                ExportToCSV(filename);
                logger.LogInfo($"Exported data to {filename}");
                MessageBox.Show($"Data exported to {filename}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                logger.LogError($"Export failed: {ex.Message}");
            }
        }

        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(logger.LogFilePath))
                {
                    Process.Start(new ProcessStartInfo(logger.LogFilePath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("Log file not found.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening log: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToCSV(string filename)
        {
            var csv = new StringBuilder();
            csv.AppendLine("World Name,Creation Time,Was Played,Time to First Activity (s)," +
                "Estimated Playtime (s),Size (bytes),Chunk Count,Modifications,DB Files,Last Modified");

            foreach (var world in currentStats.AllWorlds)
            {
                csv.AppendLine($"\"{world.WorldName}\",{world.CreationTime:yyyy-MM-dd HH:mm:ss}," +
                    $"{world.WasActuallyPlayed},{world.TimeToFirstActivity.TotalSeconds:F2}," +
                    $"{world.EstimatedPlaytime.TotalSeconds:F2},{world.TotalFileSize}," +
                    $"{world.ChunkCount},{world.ModificationCount},{world.DbFileCount}," +
                    $"{world.LastModified:yyyy-MM-dd HH:mm:ss}");
            }

            File.WriteAllText(filename, csv.ToString());
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{ts.Hours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.TotalSeconds:F1}s";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F1} {sizes[order]}";
        }
    }

    // Logger.cs
    public class Logger
    {
        public string LogFilePath { get; private set; }

        public Logger()
        {
            string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            LogFilePath = Path.Combine(logsDir, $"speedrun_analysis_{DateTime.Now:yyyyMMdd}.log");
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        public void LogDebug(string message)
        {
            WriteLog("DEBUG", message);
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch { }
        }

        public void WriteDetailedReport(SpeedrunStatistics stats)
        {
            var report = new StringBuilder();
            report.AppendLine(new string('=', 80));
            report.AppendLine($"DETAILED SPEEDRUN ANALYSIS REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine(new string('=', 80));
            report.AppendLine();

            report.AppendLine("SUMMARY STATISTICS:");
            report.AppendLine($"  Total Worlds: {stats.TotalWorlds}");
            report.AppendLine($"  Worlds Played: {stats.WorldsPlayed}");
            report.AppendLine($"  Worlds Abandoned: {stats.WorldsAbandoned}");
            report.AppendLine($"  Reset Ratio: {stats.ResetRatio:P2}");
            report.AppendLine($"  Average Playtime: {stats.AveragePlaytimePerWorld}");
            report.AppendLine($"  Average Time to First Activity: {stats.AverageTimeToFirstActivity}");
            report.AppendLine($"  Total Storage: {FormatBytes(stats.TotalStorageUsed)}");
            report.AppendLine($"  Average World Size: {FormatBytes(stats.AverageWorldSize)}");
            report.AppendLine();

            report.AppendLine("SESSION STATISTICS:");
            report.AppendLine($"  Today's Resets: {stats.TodayResets}");
            report.AppendLine($"  This Week's Resets: {stats.WeekResets}");
            report.AppendLine($"  Fastest Reset: {(stats.FastestReset.HasValue ? stats.FastestReset.Value.ToString() : "N/A")}");
            report.AppendLine($"  Longest Session: {(stats.LongestSession.HasValue ? stats.LongestSession.Value.ToString() : "N/A")}");
            report.AppendLine();

            report.AppendLine("WORLD DETAILS:");
            report.AppendLine(new string('-', 80));

            foreach (var world in stats.AllWorlds)
            {
                report.AppendLine($"World: {world.WorldName}");
                report.AppendLine($"  Path: {world.WorldPath}");
                report.AppendLine($"  Created: {world.CreationTime:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"  Last Modified: {world.LastModified:yyyy-MM-dd HH:mm:ss}");
                report.AppendLine($"  Status: {(world.WasActuallyPlayed ? "PLAYED" : "ABANDONED")}");
                report.AppendLine($"  Time to First Activity: {world.TimeToFirstActivity}");
                report.AppendLine($"  Estimated Playtime: {world.EstimatedPlaytime}");
                report.AppendLine($"  Size: {FormatBytes(world.TotalFileSize)}");
                report.AppendLine($"  Chunk Count: {world.ChunkCount}");
                report.AppendLine($"  DB Files: {world.DbFileCount}");
                report.AppendLine($"  Modification Count: {world.ModificationCount}");
                report.AppendLine($"  Detection Scores:");
                report.AppendLine($"    - Size Check: {world.TotalFileSize > 50000}");
                report.AppendLine($"    - Chunk Check: {world.ChunkCount > 5}");
                report.AppendLine($"    - Modification Check: {world.ModificationCount > 3}");
                report.AppendLine($"    - Time Check: {world.TimeToFirstActivity > TimeSpan.FromSeconds(2)}");
                report.AppendLine(new string('-', 80));
            }

            report.AppendLine();
            report.AppendLine(new string('=', 80));
            report.AppendLine();

            File.AppendAllText(LogFilePath, report.ToString());
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
    }

    // WorldStats.cs
    public class WorldStats
    {
        public string WorldName { get; set; }
        public string WorldPath { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public long TotalFileSize { get; set; }
        public int ChunkCount { get; set; }
        public int DbFileCount { get; set; }
        public TimeSpan TimeToFirstActivity { get; set; }
        public bool WasActuallyPlayed { get; set; }
        public TimeSpan EstimatedPlaytime { get; set; }
        public int ModificationCount { get; set; }
    }

    // SpeedrunStatistics.cs
    public class SpeedrunStatistics
    {
        public int TotalWorlds { get; set; }
        public int WorldsPlayed { get; set; }
        public int WorldsAbandoned { get; set; }
        public double ResetRatio { get; set; }
        public TimeSpan AveragePlaytimePerWorld { get; set; }
        public TimeSpan AverageTimeToFirstActivity { get; set; }
        public long TotalStorageUsed { get; set; }
        public long AverageWorldSize { get; set; }
        public int TodayResets { get; set; }
        public int WeekResets { get; set; }
        public TimeSpan? FastestReset { get; set; }
        public TimeSpan? LongestSession { get; set; }
        public List<WorldStats> AllWorlds { get; set; }
    }

    // MinecraftSpeedrunAnalyzer.cs
    public class MinecraftSpeedrunAnalyzer
    {
        public string WorldsPath { get; private set; }

        private const long MIN_PLAYED_SIZE = 50000;
        private const int MIN_CHUNK_FILES = 5;
        private const int MIN_MODIFICATIONS = 3;

        public MinecraftSpeedrunAnalyzer()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            WorldsPath = Path.Combine(localAppData, "Packages",
                "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftWorlds");
        }

        public MinecraftSpeedrunAnalyzer(string customPath)
        {
            WorldsPath = customPath;
        }

        public SpeedrunStatistics AnalyzeWorlds()
        {
            if (!Directory.Exists(WorldsPath))
            {
                throw new DirectoryNotFoundException($"Minecraft worlds directory not found: {WorldsPath}");
            }

            var allWorlds = new List<WorldStats>();
            var worldDirectories = Directory.GetDirectories(WorldsPath);

            foreach (var worldDir in worldDirectories)
            {
                try
                {
                    var stats = AnalyzeWorld(worldDir);
                    if (stats != null)
                    {
                        allWorlds.Add(stats);
                    }
                }
                catch { }
            }

            return CalculateStatistics(allWorlds);
        }

        private WorldStats AnalyzeWorld(string worldPath)
        {
            var stats = new WorldStats { WorldPath = worldPath };

            string levelNamePath = Path.Combine(worldPath, "levelname.txt");
            if (File.Exists(levelNamePath))
            {
                stats.WorldName = File.ReadAllText(levelNamePath).Trim();
            }
            else
            {
                stats.WorldName = Path.GetFileName(worldPath);
            }

            var dirInfo = new DirectoryInfo(worldPath);
            stats.CreationTime = dirInfo.CreationTime;
            stats.LastModified = dirInfo.LastWriteTime;
            stats.LastAccessed = dirInfo.LastAccessTime;

            stats.TotalFileSize = GetDirectorySize(worldPath);

            string dbPath = Path.Combine(worldPath, "db");
            if (Directory.Exists(dbPath))
            {
                stats.ChunkCount = Directory.GetFiles(dbPath, "*.ldb", SearchOption.TopDirectoryOnly).Length;
                stats.DbFileCount = Directory.GetFiles(dbPath, "*.*", SearchOption.TopDirectoryOnly).Length;
            }

            stats.ModificationCount = CountFileModifications(worldPath);
            stats.TimeToFirstActivity = CalculateTimeToFirstActivity(worldPath, stats.CreationTime);
            stats.WasActuallyPlayed = DetermineIfPlayed(stats);
            stats.EstimatedPlaytime = EstimatePlaytime(worldPath);

            return stats;
        }

        private long GetDirectorySize(string path)
        {
            try
            {
                return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch { return 0; }
        }

        private int CountFileModifications(string worldPath)
        {
            try
            {
                var keyFiles = new[]
                {
                    Path.Combine(worldPath, "level.dat"),
                    Path.Combine(worldPath, "level.dat_old"),
                    Path.Combine(worldPath, "db", "CURRENT")
                };

                int modCount = keyFiles.Count(f => File.Exists(f) &&
                    new FileInfo(f).LastWriteTime > new FileInfo(f).CreationTime.AddSeconds(5));

                string dbPath = Path.Combine(worldPath, "db");
                if (Directory.Exists(dbPath))
                {
                    modCount += Directory.GetFiles(dbPath, "*.ldb").Length;
                }

                return modCount;
            }
            catch { return 0; }
        }

        private TimeSpan CalculateTimeToFirstActivity(string worldPath, DateTime creationTime)
        {
            try
            {
                var earliestMod = Directory.GetFiles(worldPath, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f).LastWriteTime)
                    .Where(t => t > creationTime)
                    .OrderBy(t => t)
                    .FirstOrDefault();

                return earliestMod != default ? earliestMod - creationTime : TimeSpan.Zero;
            }
            catch { return TimeSpan.Zero; }
        }

        private bool DetermineIfPlayed(WorldStats stats)
        {
            bool sizeCheck = stats.TotalFileSize > MIN_PLAYED_SIZE;
            bool chunkCheck = stats.ChunkCount > MIN_CHUNK_FILES;
            bool modCheck = stats.ModificationCount > MIN_MODIFICATIONS;
            bool timeCheck = stats.TimeToFirstActivity > TimeSpan.FromSeconds(2);

            int criteriaCount = (sizeCheck ? 1 : 0) + (chunkCheck ? 1 : 0) +
                               (modCheck ? 1 : 0) + (timeCheck ? 1 : 0);

            return criteriaCount >= 2;
        }

        private TimeSpan EstimatePlaytime(string worldPath)
        {
            try
            {
                var modTimes = Directory.GetFiles(worldPath, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f).LastWriteTime)
                    .OrderBy(t => t)
                    .ToList();

                if (modTimes.Count < 2)
                    return TimeSpan.Zero;

                TimeSpan totalTime = TimeSpan.Zero;
                for (int i = 1; i < modTimes.Count; i++)
                {
                    var gap = modTimes[i] - modTimes[i - 1];
                    if (gap < TimeSpan.FromMinutes(5))
                    {
                        totalTime += gap;
                    }
                }

                return totalTime;
            }
            catch { return TimeSpan.Zero; }
        }

        private SpeedrunStatistics CalculateStatistics(List<WorldStats> worlds)
        {
            var playedWorlds = worlds.Where(w => w.WasActuallyPlayed).ToList();
            var abandonedWorlds = worlds.Where(w => !w.WasActuallyPlayed).ToList();

            var today = DateTime.Today;
            var weekAgo = today.AddDays(-7);

            var todayResets = abandonedWorlds.Count(w => w.CreationTime >= today);
            var weekResets = abandonedWorlds.Count(w => w.CreationTime >= weekAgo);

            var fastestReset = abandonedWorlds
                .Where(w => w.TimeToFirstActivity > TimeSpan.Zero && w.TimeToFirstActivity < TimeSpan.FromMinutes(1))
                .OrderBy(w => w.TimeToFirstActivity)
                .FirstOrDefault()?.TimeToFirstActivity;

            var longestSession = playedWorlds
                .Where(w => w.EstimatedPlaytime > TimeSpan.Zero)
                .OrderByDescending(w => w.EstimatedPlaytime)
                .FirstOrDefault()?.EstimatedPlaytime;

            return new SpeedrunStatistics
            {
                TotalWorlds = worlds.Count,
                WorldsPlayed = playedWorlds.Count,
                WorldsAbandoned = abandonedWorlds.Count,
                ResetRatio = worlds.Count > 0 ? (double)abandonedWorlds.Count / worlds.Count : 0,
                AveragePlaytimePerWorld = playedWorlds.Any()
                    ? TimeSpan.FromSeconds(playedWorlds.Average(w => w.EstimatedPlaytime.TotalSeconds))
                    : TimeSpan.Zero,
                AverageTimeToFirstActivity = worlds.Any()
                    ? TimeSpan.FromSeconds(worlds.Average(w => w.TimeToFirstActivity.TotalSeconds))
                    : TimeSpan.Zero,
                TotalStorageUsed = worlds.Sum(w => w.TotalFileSize),
                AverageWorldSize = worlds.Any() ? (long)worlds.Average(w => w.TotalFileSize) : 0,
                TodayResets = todayResets,
                WeekResets = weekResets,
                FastestReset = fastestReset,
                LongestSession = longestSession,
                AllWorlds = worlds.OrderByDescending(w => w.CreationTime).ToList()
            };
        }
    }
}
