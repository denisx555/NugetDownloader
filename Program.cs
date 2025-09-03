using System.CommandLine;
using System.Xml.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net;

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

            rootCommand.AddOption(directoryPackagesPropsPathOption);
            rootCommand.AddOption(outputDirectoryOption);
            rootCommand.AddOption(nugetSourcesOption);
            rootCommand.AddOption(disableSslValidationOption);
            rootCommand.AddOption(usernameOption);
            rootCommand.AddOption(passwordOption);

            rootCommand.SetHandler(async (propsPath, outputDir, sources, disableSslValidation, user, pass) =>
            {
                await DownloadPackages(propsPath, outputDir, sources, disableSslValidation, user, pass);
            },
            directoryPackagesPropsPathOption, outputDirectoryOption, nugetSourcesOption, disableSslValidationOption, usernameOption, passwordOption);

            await rootCommand.InvokeAsync(args);
        }

        static async Task DownloadPackages(FileInfo propsPath, DirectoryInfo outputDir, string[] nugetSources, bool disableSslValidation, string? username, string? password)
        {
            var allSources = nugetSources.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
            Console.WriteLine($"Reading {propsPath.FullName}...");

            if (!propsPath.Exists)
            {
                Console.WriteLine($"Error: File not found at {propsPath.FullName}");
                return;
            }

            if (!outputDir.Exists)
            {
                Console.WriteLine($"Creating output directory: {outputDir.FullName}");
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
                Console.WriteLine($"Error parsing {propsPath.FullName}: {ex.Message}");
                return;
            }

            var packagesToDownload = packages
                .Where(p => !File.Exists(Path.Combine(outputDir.FullName, $"{p.Id}.{p.Version}.nupkg")))
                .ToList();

            Console.WriteLine($"Found {packages.Count} total packages. Need to download {packagesToDownload.Count}.");

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
                    Console.WriteLine($"→ Checking {source} for {id}.{version}");
                    string downloadUrl = "";

                    if (source.Contains("api.nuget.org/v3-flatcontainer", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = $"{source.TrimEnd('/')}/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.{version}.nupkg";
                    }
                    else
                    {
                        downloadUrl = $"{source.TrimEnd('/')}/{id}/{version}";
                    }

                    try
                    {
                        var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        if (response.IsSuccessStatusCode)
                        {
                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await contentStream.CopyToAsync(fileStream, cancellationToken);
                            }
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✔ {id}.{version} (downloaded from {source})");
                            Console.ResetColor();
                            downloaded = true;
                            break;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"✖ {id}.{version} (failed from {source}: {response.StatusCode})");
                            Console.ResetColor();
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✖ {id}.{version} (HTTP error from {source}: {httpEx.Message})");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✖ {id}.{version} (error from {source}: {ex.Message})");
                        Console.ResetColor();
                    }
                }

                if (!downloaded)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"‼ {id}.{version} (not found in any source)");
                    Console.ResetColor();
                }
            });

            Console.WriteLine("Download process completed.");
        }
    }
}