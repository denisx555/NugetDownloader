using System.IO;

namespace NugetDownloader
{
    public class DownloadOptions
    {
        public FileInfo PropsPath { get; set; } = null!;
        public DirectoryInfo OutputDir { get; set; } = null!;
        public string[] NugetSources { get; set; } = null!;
        public bool DisableSslValidation { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public FileInfo? LogFile { get; set; }
    }
}