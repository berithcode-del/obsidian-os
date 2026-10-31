using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianInstaller.Core;

public sealed record HardwareProfile(
    [property: JsonPropertyName("profile_id")] string? ProfileId,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("validation_level")] string? ValidationLevel,
    [property: JsonPropertyName("customer_installation_allowed")] bool CustomerInstallationAllowed,
    [property: JsonPropertyName("usb_ids")] HardwareProfileUsbIds? UsbIds);

public sealed record HardwareProfileUsbIds(
    [property: JsonPropertyName("fel")] string? Fel,
    [property: JsonPropertyName("bootstrap_ums")] string? BootstrapUms,
    [property: JsonPropertyName("running_system")] string? RunningSystem);

public sealed record HardwareProfileCandidate(
    string ProfileId,
    string DisplayName,
    string ValidationLevel,
    bool CustomerInstallationAllowed,
    string MatchedMode);

public sealed record HardwareProfileCatalogLoad(
    HardwareProfileCatalog Catalog,
    IReadOnlyList<string> Issues);

/// <summary>
/// Reads the local hardware-profile catalog and compares declared USB IDs.
/// A match is informational only and never confirms a physical board.
/// </summary>
public sealed class HardwareProfileCatalog
{
    private const long MaximumProfileBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };
    private readonly IReadOnlyList<HardwareProfile> _profiles;

    private HardwareProfileCatalog(IReadOnlyList<HardwareProfile> profiles) => _profiles = profiles;

    public IReadOnlyList<HardwareProfile> Profiles => _profiles;

    public static HardwareProfileCatalogLoad Load(string directory)
    {
        var profiles = new List<HardwareProfile>();
        var issues = new List<string>();
        var identities = new HashSet<string>(StringComparer.Ordinal);

        if (!Directory.Exists(directory))
            return new(new HardwareProfileCatalog(profiles), [$"Diretório de perfis não encontrado: {directory}"]);

        string[] paths;
        try
        {
            paths = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return new(new HardwareProfileCatalog(profiles), ["O diretório de perfis não pôde ser lido."]);
        }
        catch (UnauthorizedAccessException)
        {
            return new(new HardwareProfileCatalog(profiles), ["A leitura do diretório de perfis foi negada pelo Windows."]);
        }

        foreach (var path in paths)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length is <= 0 or > MaximumProfileBytes)
                {
                    issues.Add($"{Path.GetFileName(path)}: tamanho fora do limite de 1 MiB.");
                    continue;
                }

                var profile = JsonSerializer.Deserialize<HardwareProfile>(File.ReadAllText(path), JsonOptions);
                if (!IsValid(profile))
                {
                    issues.Add($"{Path.GetFileName(path)}: perfil incompleto ou com IDs USB inválidos.");
                    continue;
                }

                if (!identities.Add(profile!.ProfileId!))
                {
                    issues.Add($"{Path.GetFileName(path)}: identificador de perfil duplicado.");
                    continue;
                }

                profiles.Add(profile);
            }
            catch (JsonException)
            {
                issues.Add($"{Path.GetFileName(path)}: JSON inválido.");
            }
            catch (IOException)
            {
                issues.Add($"{Path.GetFileName(path)}: arquivo não pôde ser lido.");
            }
            catch (UnauthorizedAccessException)
            {
                issues.Add($"{Path.GetFileName(path)}: acesso ao arquivo negado.");
            }
        }

        if (profiles.Count == 0)
            issues.Add("O catálogo não contém perfis utilizáveis.");

        return new(new HardwareProfileCatalog(profiles), issues);
    }

    public IReadOnlyList<HardwareProfileCandidate> FindCandidates(string vendorId, string productId)
    {
        var targetId = $"{vendorId}:{productId}".ToUpperInvariant();
        var candidates = new List<HardwareProfileCandidate>();

        foreach (var profile in _profiles)
        {
            var ids = profile.UsbIds!;
            var modes = new (string? Id, string Label)[]
            {
                (ids.Fel, "FEL da ROM"),
                (ids.BootstrapUms, "Armazenamento USB temporário"),
                (ids.RunningSystem, "Sistema iniciado")
            };

            foreach (var (id, mode) in modes)
            {
                if (!string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidates.Add(new HardwareProfileCandidate(
                    profile.ProfileId!, profile.DisplayName!, profile.ValidationLevel!,
                    profile.CustomerInstallationAllowed, mode));
                break;
            }
        }

        return candidates;
    }

    private static bool IsValid(HardwareProfile? profile)
    {
        if (profile is null
            || string.IsNullOrWhiteSpace(profile.ProfileId)
            || string.IsNullOrWhiteSpace(profile.DisplayName)
            || string.IsNullOrWhiteSpace(profile.ValidationLevel)
            || profile.UsbIds is null)
            return false;

        var ids = new[] { profile.UsbIds.Fel, profile.UsbIds.BootstrapUms, profile.UsbIds.RunningSystem };
        return ids.All(id => id is { Length: 9 }
            && id[4] == ':'
            && id.Where((_, index) => index != 4).All(Uri.IsHexDigit));
    }
}
