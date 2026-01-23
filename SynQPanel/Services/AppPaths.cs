using System;
using System.IO;
using SynQPanel.Models;

namespace SynQPanel.Infrastructure
{
    public static class AppPaths
    {
        public static string DataRoot { get; private set; } = string.Empty;

        public static string Logs => Path.Combine(DataRoot, "logs");
        public static string Profiles => Path.Combine(DataRoot, "profiles");
        public static string Assets => Path.Combine(DataRoot, "assets");
        public static string Cache => Path.Combine(DataRoot, "cache");

        public static void Initialize(Settings settings)
        {
            DataRoot = settings.DataRootPath;

            // IMPORTANT: do NOT migrate yet
            EnsureDirectories();
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(Logs);
            Directory.CreateDirectory(Profiles);
            Directory.CreateDirectory(Assets);
            Directory.CreateDirectory(Cache);
        }
    }
}
