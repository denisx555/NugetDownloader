using System.CommandLine;
using System.Xml.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net;
using System.IO;

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

            rootCommand.SetHandler(async (propsPath, outputDir, sources, disableSslValidation, user, pass, logFile) =>
            {
                await DownloadPackages(propsPath, outputDir, sources, disableSslValidation, user, pass, logFile);
            },
            directoryPackagesPropsPathOption, outputDirectoryOption, nugetSourcesOption, disableSslValidationOption, usernameOption, passwordOption, logFileOption);

            await rootCommand.InvokeAsync(args);
        }

        static async Task DownloadPackages(FileInfo propsPath, DirectoryInfo outputDir, string[] nugetSources, bool disableSslValidation, string? username, string? password, FileInfo? logFile)
        {
            // Clear the log file on a new run if it's specified
            if (logFile != null && File.Exists(logFile.FullName))
            {
                File.Delete(logFile.FullName);
            }

            var logLock = new object();

            Action<string, ConsoleColor?> log = (message, color) =>
            {
                lock (logLock)
                {
                    if (color.HasValue)
                    {
                        Console.ForegroundColor = color.Value;
                        Console.WriteLine(message);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine(message);
                    }

                    if (logFile != null)
                    {
                        File.AppendAllText(logFile.FullName, $"[{System.DateTime.UtcNow:O}] {message}\n");
                    }
                }
            };

            var allSources = nugetSources.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
            log($"Reading {propsPath.FullName}...", null);

            if (!propsPath.Exists)
            {
                log($"Error: File not found at {propsPath.FullName}", ConsoleColor.Red);
                return;
            }

            if (!outputDir.Exists)
            {
                log($"Creating output directory: {outputDir.FullName}", null);
                outputDir.Create();
            }

            var packages = new List<(string Id, string Version)>();
            try
            {
                var doc = XDocument.Load(propsPath.FullName);
                var packageVersionElements = doc.Descendants("PackageVersion");

                foreach (var element in packageVersionElements)
                {
                    var id = element.Attribute("Include")?.Value;
                    var version = element.Attribute("Version")?.Value;

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version))
                    {
                        packages.Add((id, version));
                    }
                }
            }
            catch (Exception ex)
            {
                log($"Error parsing {propsPath.FullName}: {ex.Message}", ConsoleColor.Red);
                return;
            }

            var packagesToDownload = packages
                .Where(p => !File.Exists(Path.Combine(outputDir.FullName, $"{p.Id}.{p.Version}.nupkg")))
                .ToList();

            log($"Found {packages.Count} total packages. Need to download {packagesToDownload.Count}.", null);

            var httpClientHandler = new HttpClientHandler();
            if (disableSslValidation)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
            }

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                httpClientHandler.Credentials = new NetworkCredential(username, password);
            }

            using var httpClient = new HttpClient(httpClientHandler);

            await Parallel.ForEachAsync(packagesToDownload, async (package, cancellationToken) =>
            {
                var (id, version) = package;
                var packageFileName = $"{id}.{version}.nupkg";
                var localFilePath = Path.Combine(outputDir.FullName, packageFileName);

                bool downloaded = false;
                foreach (var source in allSources)
                {
                    log($"→ Checking {source} for {id}.{version}", null);
                    string downloadUrl = "";

                    if (source.Contains("api.nuget.org/v3-flatcontainer", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = $"{source.TrimEnd('/')}/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.{version}.nupkg";
                    }
                    else
                    {
                        downloadUrl = $"{source.TrimEnd('/')}/{id}/{version}";
                    }

                    log($"  [DEBUG] Attempting to download from URL: {downloadUrl}", ConsoleColor.Cyan);

                    try
                    {
                        var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                            using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await contentStream.CopyToAsync(fileStream, cancellationToken);
                            }
                            log($"✔ {id}.{version} (downloaded from {source})", ConsoleColor.Green);
                            downloaded = true;
                            break;
                        }
                        else
                        {
                            log($"✖ {id}.{version} (failed from {source}: {response.StatusCode})", ConsoleColor.Red);
                            log($"  [DEBUG] Response Headers: {response.Headers.ToString().Replace("\r\n", " ")}", ConsoleColor.Yellow);
                            if (response.Content.Headers.ContentType != null)
                            {
                                log($"  [DEBUG] Content-Type: {response.Content.Headers.ContentType}", ConsoleColor.Yellow);
                            }
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        log($"✖ {id}.{version} (HTTP error from {source}: {httpEx.Message})", ConsoleColor.Red);
                    }
                    catch (Exception ex)
                    {
                        log($"✖ {id}.{version} (error from {source}: {ex.Message})", ConsoleColor.Red);
                    }
                }

                if (!downloaded)
                {
                    log($"‼ {id}.{version} (not found in any source)", ConsoleColor.Red);
                }
            });

            log("Download process completed.", null);
            if (logFile != null)
            {
                log($"Log file written to {logFile.FullName}", null);
            }
        }
    }
}