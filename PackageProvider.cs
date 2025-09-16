using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NugetDownloader
{
    public static class PackageProvider
    {
        public static List<(string Id, string Version)> GetPackages(FileInfo propsPath, AppLogger logger)
        {
            var packages = new List<(string Id, string Version)>();
            var properties = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            logger.Log($"Reading {propsPath.FullName}...", null);

            if (!propsPath.Exists)
            {
                logger.Log($"Error: File not found at {propsPath.FullName}", System.ConsoleColor.Red);
                return packages;
            }

            try
            {
                var doc = XDocument.Load(propsPath.FullName);

                // Parse PropertyGroups to find variables
                foreach (var pg in doc.Descendants("PropertyGroup"))
                {
                    foreach (var prop in pg.Elements())
                    {
                        string propName = prop.Name.LocalName;
                        string propValue = prop.Value;
                        // Simple handling for "define if not exists" found in props files
                        if (!properties.ContainsKey(propName))
                        {
                            properties[propName] = propValue;
                        }
                    }
                }

                var packageVersionElements = doc.Descendants("PackageVersion");

                foreach (var element in packageVersionElements)
                {
                    var id = element.Attribute("Include")?.Value;
                    var version = element.Attribute("Version")?.Value;

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version))
                    {
                        // Substitute MSBuild properties like $(PropertyName)
                        var finalVersion = Regex.Replace(version, @"\$\((.*?)\)", match =>
                        {
                            string varName = match.Groups[1].Value;
                            return properties.TryGetValue(varName, out var val) ? val : match.Value;
                        });

                        packages.Add((id, finalVersion.Trim()));
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Log($"Error parsing {propsPath.FullName}: {ex.Message}", System.ConsoleColor.Red);
            }

            return packages;
        }
    }
}