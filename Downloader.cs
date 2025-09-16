using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace NugetDownloader
{
    public class Downloader
    {
        private readonly DownloadOptions _options;
        private readonly AppLogger _logger;

        public Downloader(DownloadOptions options, AppLogger logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task DownloadPackagesAsync()
        {
            var allSources = _options.NugetSources.SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();

            if (!_options.OutputDir.Exists)
            {
                _logger.Log($"Creating output directory: {_options.OutputDir.FullName}");
                _options.OutputDir.Create();
            }

            var packages = PackageProvider.GetPackages(_options.PropsPath, _logger);
            if (!packages.Any())
                return;

            var packagesToDownload = packages
                .Where(p => !File.Exists(Path.Combine(_options.OutputDir.FullName, $"{p.Id}.{p.Version}.nupkg")))
                .ToList();

            _logger.Log($"Found {packages.Count} total packages. Need to download {packagesToDownload.Count}.");

            var httpClientHandler = new HttpClientHandler();
            if (_options.DisableSslValidation)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };                
            }

            if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
            {
                httpClientHandler.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            using var httpClient = new HttpClient(httpClientHandler);

            await Parallel.ForEachAsync(packagesToDownload, async (package, cancellationToken) =>
            {
                await DownloadPackage(httpClient, package, allSources, cancellationToken);
            });

            _logger.Log("Download process completed.");
            if (_options.LogFile != null)
            {
                _logger.Log($"Log file written to {_options.LogFile.FullName}");
            }
        }

        private async Task DownloadPackage(HttpClient httpClient, (string Id, string Version) package, string[] sources, CancellationToken cancellationToken)
        {
            var (id, version) = package;
            var packageFileName = $"{id}.{version}.nupkg";
            var localFilePath = Path.Combine(_options.OutputDir.FullName, packageFileName);
            bool downloaded = false;

            foreach (var source in sources)
            {
                _logger.Log($"→ Checking {source} for {id}.{version}");
                string downloadUrl;

                if (source.Contains("api.nuget.org/v3-flatcontainer", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = $"{source.TrimEnd('/')}/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.{version}.nupkg";
                }
                else
                {
                    downloadUrl = $"{source.TrimEnd('/')}/{id}/{version}";
                }

                _logger.Log($"  [DEBUG] Attempting to download from URL: {downloadUrl}", ConsoleColor.Cyan);

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
                        _logger.Log($"✔ {id}.{version} (downloaded from {source})", ConsoleColor.Green);
                        downloaded = true;
                        break;
                    }
                    else
                    {
                        _logger.Log($"✖ {id}.{version} (failed from {source}: {response.StatusCode})", ConsoleColor.Red);
                        _logger.Log($"  [DEBUG] Response Headers: {response.Headers.ToString().Replace("\r\n", " ")}", ConsoleColor.Yellow);
                        if (response.Content.Headers.ContentType != null)
                        {
                            _logger.Log($"  [DEBUG] Content-Type: {response.Content.Headers.ContentType}", ConsoleColor.Yellow);
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.Log($"✖ {id}.{version} (HTTP error from {source}: {httpEx.Message})", ConsoleColor.Red);
                    if (httpEx.InnerException != null)
                    {
                        _logger.Log($"  [DEBUG] Inner Exception: {httpEx.InnerException}", ConsoleColor.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"✖ {id}.{version} (error from {source}: {ex.Message})", ConsoleColor.Red);
                }
            }

            if (!downloaded)
            {
                _logger.Log($"‼ {id}.{version} (not found in any source)", ConsoleColor.Red);
            }
        }
    }
}