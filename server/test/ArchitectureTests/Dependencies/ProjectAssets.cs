using System.Text.Json;

namespace Metaspesa.ArchitectureTests.Dependencies;

internal static class ProjectAssets {
  internal static readonly string[] Projects = [
    "Domain",
    "Application",
    "Database",
    "Infrastructure",
    "RestApi",
    "MigrationService",
    "ServiceDefaults",
  ];

  internal static JsonDocument Read(string project) => JsonDocument.Parse(File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "DependencyMetadata", project + ".assets.json")));

  internal static string[] ProjectReferences(JsonElement assets) => [.. assets
    .GetProperty("project")
    .GetProperty("restore")
    .GetProperty("frameworks")
    .EnumerateObject()
    .SelectMany(framework => framework.Value
      .GetProperty("projectReferences")
      .EnumerateObject())
    .Select(reference => Path.GetFileNameWithoutExtension(
      reference.Name.Replace('\\', '/')))
    .Distinct(StringComparer.Ordinal)
  ];

  internal static string[] DirectPackages(JsonElement assets) => [.. assets
    .GetProperty("project")
    .GetProperty("frameworks")
    .EnumerateObject()
    .Where(framework => framework.Value.TryGetProperty("dependencies", out _))
    .SelectMany(framework => framework.Value
      .GetProperty("dependencies")
      .EnumerateObject())
    .Select(package => package.Name).Distinct(StringComparer.Ordinal)
  ];

  internal static string[] RuntimePackages(JsonElement assets) => [.. assets
    .GetProperty("targets")
    .EnumerateObject()
    .SelectMany(target => target.Value.EnumerateObject())
    .Where(library => library.Value.GetProperty("type").GetString() == "package" &&
      (library.Value.TryGetProperty("compile", out _) || library.Value.TryGetProperty("runtime", out _)))
    .Select(library => library.Name.Split('/')[0])
    .Distinct(StringComparer.Ordinal)
  ];
}