using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace NugetDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand("A tool to download NuGet packages based on Directory.Packages.props");

            var directoryPackagesPropsPathOption = new Option<FileInfo>(
                name: "--props-path",
                description: "Path to the Directory.Packages.props file.")
            { IsRequired = true };

            var outputDirectoryOption = new Option<DirectoryInfo>(
                name: "--output-dir",
                description: "Local directory to save downloaded packages.")
            { IsRequired = true };

            var nugetSourcesOption = new Option<string[]>(
                name: "--sources",
                description: "Comma-separated list of NuGet repository URLs.")
            { IsRequired = true, Arity = ArgumentArity.OneOrMore };

            var disableSslValidationOption = new Option<bool>(
                name: "--disable-ssl-validation",
                description: "Disable SSL certificate validation.",
                getDefaultValue: () => false);

            var usernameOption = new Option<string>(
                name: "--user",
                description: "Username for the private repository.");

            var passwordOption = new Option<string>(
                name: "--password",
                description: "Password for the private repository.");

            var logFileOption = new Option<FileInfo>(
                name: "--log-file",
                description: "Optional path to a log file.");

            rootCommand.AddOption(directoryPackagesPropsPathOption);
            rootCommand.AddOption(outputDirectoryOption);
            rootCommand.AddOption(nugetSourcesOption);
            rootCommand.AddOption(disableSslValidationOption);
            rootCommand.AddOption(usernameOption);
            rootCommand.AddOption(passwordOption);
            rootCommand.AddOption(logFileOption);

            rootCommand.SetHandler(async (options) =>
            {
                var logger = new AppLogger(options.LogFile);
                var downloader = new Downloader(options, logger);
                await downloader.DownloadPackagesAsync();
            },
            new DownloadOptionsBinder(directoryPackagesPropsPathOption, outputDirectoryOption, nugetSourcesOption, disableSslValidationOption, usernameOption, passwordOption, logFileOption));

            await rootCommand.InvokeAsync(args);
        }
    }
}
