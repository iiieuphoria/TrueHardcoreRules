using SPTarkov.Server.Core.Models.Spt.Mod;

namespace TrueHardcoreRules;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.truehardcorerules.server";
    public string Name { get; init; } = "TrueHardcoreRules";
    public string Author { get; init; } = "mjasi";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0", false);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0", false);
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
