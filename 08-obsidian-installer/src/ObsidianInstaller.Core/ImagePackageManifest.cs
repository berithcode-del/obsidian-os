using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianInstaller.Core;

public sealed class ImagePackageManifest
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("package_id")] public string? PackageId { get; init; }
    [JsonPropertyName("display_name")] public string? DisplayName { get; init; }
    [JsonPropertyName("system_version")] public string? SystemVersion { get; init; }
    [JsonPropertyName("channel")] public string? Channel { get; init; }
    [JsonPropertyName("customer_installation_allowed")] public bool CustomerInstallationAllowed { get; init; }
    [JsonPropertyName("distribution_review")] public string? DistributionReview { get; init; }
    [JsonPropertyName("compatible_profile_ids")] public List<string>? CompatibleProfileIds { get; init; }
    [JsonPropertyName("payload")] public ImagePayloadManifest? Payload { get; init; }
    [JsonPropertyName("sector_bytes")] public int SectorBytes { get; init; }
}

public sealed class ImagePayloadManifest
{
    [JsonPropertyName("kind")] public string? Kind { get; init; }
    [JsonPropertyName("asset_id")] public string? AssetId { get; init; }
    [JsonPropertyName("file")] public string? File { get; init; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; init; }
    [JsonPropertyName("image_bytes")] public long ImageBytes { get; init; }
}

public sealed record PackageManifestInspection(
    string ManifestPath,
    ImagePackageManifest? Manifest,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Notes)
{
    public PackageSignatureVerification Signature { get; init; } = new(
        PackageSignatureStatus.NotChecked, null, "Assinatura ainda não conferida.");

    /// <summary>Whether the user-selected image may be read and compared with the manifest.</summary>
    public bool CanVerifyImage => Manifest is not null && Issues.Count == 0;

    /// <summary>Installation is fail-closed until a trusted signature and release policy are verified.</summary>
    public bool CanInstall => false;
}

public sealed record ImageFileVerification(
    long ExpectedBytes,
    long ActualBytes,
    string? ExpectedSha256,
    string? ActualSha256,
    bool SizeMatches,
    bool HashMatches)
{
    public bool Matches => SizeMatches && HashMatches;
}

/// <summary>Inspects local package metadata and a user-selected image file. It never opens a disk device.</summary>
public sealed class ImagePackageInspector
{
    private const long MaximumManifestBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

    public PackageManifestInspection Inspect(string manifestPath, string profileDirectory, string trustedKeyCatalogPath)
    {
        var issues = new List<string>();
        var notes = new List<string>
        {
            "A prévia confere metadados, hash e assinatura do manifesto contra o catálogo local; nenhum desses controles isolados autoriza instalação."
        };

        try
        {
            var info = new FileInfo(manifestPath);
            if (!info.Exists)
                return new(manifestPath, null, ["O manifesto selecionado não foi encontrado."], notes);
            if (info.Length is <= 0 or > MaximumManifestBytes)
                return new(manifestPath, null, ["O manifesto deve ter entre 1 byte e 1 MiB."], notes);

            var manifestBytes = File.ReadAllBytes(manifestPath);
            var jsonBytes = manifestBytes.AsSpan();
            if (jsonBytes.Length >= 3 && jsonBytes[0] == 0xEF && jsonBytes[1] == 0xBB && jsonBytes[2] == 0xBF)
                jsonBytes = jsonBytes[3..];
            var manifest = JsonSerializer.Deserialize<ImagePackageManifest>(jsonBytes, JsonOptions);
            if (manifest is null)
                return new(manifestPath, null, ["O arquivo não contém um manifesto reconhecido."], notes);

            if (manifest.SchemaVersion != 1) issues.Add("A versão do formato do manifesto não é suportada.");
            if (string.IsNullOrWhiteSpace(manifest.PackageId)) issues.Add("Falta o identificador do pacote.");
            if (string.IsNullOrWhiteSpace(manifest.DisplayName)) issues.Add("Falta o nome de exibição do pacote.");
            if (string.IsNullOrWhiteSpace(manifest.SystemVersion)) issues.Add("Falta a versão do sistema.");
            if (manifest.Channel is not ("lab" or "candidate" or "beta" or "stable")) issues.Add("O canal do pacote não é reconhecido.");
            if (manifest.CustomerInstallationAllowed) issues.Add("Este aplicativo não aceita pacotes que declarem instalação para clientes.");
            if (manifest.DistributionReview != "approved") notes.Add("A revisão de distribuição deste manifesto não está aprovada.");
            if (manifest.CompatibleProfileIds is not { Count: > 0 })
                issues.Add("O manifesto não declara perfis de hardware.");
            else
            {
                var profileCatalog = HardwareProfileCatalog.Load(profileDirectory);
                var knownProfiles = profileCatalog.Catalog.Profiles
                    .Select(profile => profile.ProfileId!)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var catalogIssue in profileCatalog.Issues)
                    notes.Add($"Catálogo de perfis: {catalogIssue}");
                foreach (var profileId in manifest.CompatibleProfileIds.Distinct(StringComparer.Ordinal))
                    if (!knownProfiles.Contains(profileId))
                        issues.Add($"O perfil {profileId} não está no catálogo local deste aplicativo.");
                    else if (profileCatalog.Catalog.Profiles.First(profile => profile.ProfileId == profileId).CustomerInstallationAllowed is false)
                        notes.Add($"O perfil {profileId} não está liberado para instalação de clientes.");
                notes.Add("Um perfil no catálogo local não significa que o aparelho esteja homologado para instalação comercial.");
            }

            var payload = manifest.Payload;
            if (payload is null)
                issues.Add("Falta a seção da imagem do sistema.");
            else
            {
                if (payload.Kind == "external_reference" && string.IsNullOrWhiteSpace(payload.AssetId))
                    issues.Add("A referência externa da imagem não tem identificador.");
                else if (payload.Kind == "bundled_file" && string.IsNullOrWhiteSpace(payload.File))
                    issues.Add("A imagem do pacote não tem caminho de arquivo.");
                else if (payload.Kind is not ("external_reference" or "bundled_file"))
                    issues.Add("O tipo da imagem do pacote não é reconhecido.");
                if (payload.Sha256 is null || payload.Sha256.Length != 64 || payload.Sha256.Any(c => !Uri.IsHexDigit(c)))
                    issues.Add("O SHA-256 declarado no manifesto é inválido.");
                if (payload.ImageBytes < 512 || manifest.SectorBytes is not (512 or 4096)
                    || payload.ImageBytes % manifest.SectorBytes != 0)
                    issues.Add("O tamanho da imagem ou do setor não é válido.");
            }

            if (manifest.Channel == "lab") notes.Add("Este é um pacote de laboratório, bloqueado para uso comercial.");
            if (manifest.DistributionReview == "approved") notes.Add("A aprovação declarada no JSON é informativa e não foi autenticada por esta prévia.");
            var signature = PackageSignatureVerifier.Verify(manifestBytes, manifestPath, trustedKeyCatalogPath);
            notes.Add(signature.Message);
            return new(manifestPath, manifest, issues, notes) { Signature = signature };
        }
        catch (JsonException)
        {
            return new(manifestPath, null, ["O JSON do manifesto está inválido ou incompleto."], notes);
        }
        catch (IOException)
        {
            return new(manifestPath, null, ["O manifesto não pôde ser lido."], notes);
        }
        catch (UnauthorizedAccessException)
        {
            return new(manifestPath, null, ["O Windows não permitiu ler o manifesto selecionado."], notes);
        }
    }

    public async Task<ImageFileVerification> VerifyImageAsync(ImagePackageManifest manifest, string imagePath,
        IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        var payload = manifest.Payload ?? throw new InvalidDataException("O manifesto não declara a imagem.");
        await using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 1024, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualBytes = stream.Length;
        if (actualBytes != payload.ImageBytes)
            return new(payload.ImageBytes, actualBytes, payload.Sha256, null, false, false);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        long lastReportedPosition = 0;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, bytesRead);
            if (stream.Position - lastReportedPosition >= 32L * 1024 * 1024)
            {
                progress?.Report(stream.Position);
                lastReportedPosition = stream.Position;
            }
        }

        progress?.Report(actualBytes);
        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        var matches = string.Equals(actualHash, payload.Sha256, StringComparison.OrdinalIgnoreCase);
        return new(payload.ImageBytes, actualBytes, payload.Sha256, actualHash, true, matches);
    }

}
