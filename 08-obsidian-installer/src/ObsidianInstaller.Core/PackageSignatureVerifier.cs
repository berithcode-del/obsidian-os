using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsidianInstaller.Core;

public enum PackageSignatureStatus
{
    NotChecked,
    SignatureMissing,
    TrustCatalogUnavailable,
    NoTrustedKeys,
    MalformedSignature,
    UnknownKey,
    RevokedKey,
    UnsupportedAlgorithm,
    InvalidTrustCatalogKey,
    InvalidSignature,
    TrustedSignature
}

public sealed record PackageSignatureVerification(
    PackageSignatureStatus Status,
    string? KeyId,
    string Message)
{
    public bool IsTrusted => Status == PackageSignatureStatus.TrustedSignature;
}

public sealed class TrustedPackageKeyCatalog
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("keys")] public List<TrustedPackageKey>? Keys { get; init; }
}

public sealed class TrustedPackageKey
{
    [JsonPropertyName("key_id")] public string? KeyId { get; init; }
    [JsonPropertyName("algorithm")] public string? Algorithm { get; init; }
    [JsonPropertyName("public_key_pem")] public string? PublicKeyPem { get; init; }
    [JsonPropertyName("revoked")] public bool? Revoked { get; init; }
}

public sealed class PackageSignatureEnvelope
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; }
    [JsonPropertyName("algorithm")] public string? Algorithm { get; init; }
    [JsonPropertyName("key_id")] public string? KeyId { get; init; }
    [JsonPropertyName("signature_base64")] public string? SignatureBase64 { get; init; }
}

/// <summary>Verifies a detached ECDSA P-256 signature against keys pinned by the application.</summary>
public static class PackageSignatureVerifier
{
    public const string Algorithm = "ecdsa-p256-sha256-der";
    private const int MaximumSignatureBytes = 16 * 1024;
    private const int MaximumTrustCatalogBytes = 128 * 1024;
    private const int MaximumSignatureValueBytes = 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <param name="manifestBytes">Exact bytes of the manifest, including any byte-order mark.</param>
    /// <param name="manifestPath">Path to the manifest; the detached signature is appended as .sig.json.</param>
    /// <param name="trustedKeyCatalogPath">Key catalog shipped with the application, not with the image package.</param>
    public static PackageSignatureVerification Verify(ReadOnlySpan<byte> manifestBytes, string manifestPath,
        string trustedKeyCatalogPath)
    {
        var signaturePath = manifestPath + ".sig.json";
        if (!File.Exists(signaturePath))
            return Result(PackageSignatureStatus.SignatureMissing, null,
                "Assinatura do manifesto não encontrada; a origem do pacote não pode ser autenticada.");

        TrustedPackageKeyCatalog? catalog;
        try
        {
            var catalogInfo = new FileInfo(trustedKeyCatalogPath);
            if (!catalogInfo.Exists || catalogInfo.Length is <= 0 or > MaximumTrustCatalogBytes)
                return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                    "Catálogo local de chaves confiáveis ausente ou fora do limite.");

            catalog = JsonSerializer.Deserialize<TrustedPackageKeyCatalog>(File.ReadAllBytes(trustedKeyCatalogPath), JsonOptions);
        }
        catch (JsonException)
        {
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "Catálogo local de chaves confiáveis contém JSON inválido.");
        }
        catch (IOException)
        {
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "Não foi possível ler o catálogo local de chaves confiáveis.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "A leitura do catálogo local de chaves confiáveis foi negada pelo Windows.");
        }

        if (catalog is not { SchemaVersion: 1, Keys: not null })
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "Formato do catálogo local de chaves confiáveis não suportado.");
        if (catalog.Keys.Count > 32)
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "O catálogo excede o limite de 32 chaves confiáveis.");
        if (catalog.Keys.Count == 0)
            return Result(PackageSignatureStatus.NoTrustedKeys, null,
                "Nenhuma chave de publicação oficial está configurada neste aplicativo.");
        if (catalog.Keys.Any(key => string.IsNullOrWhiteSpace(key.KeyId))
            || catalog.Keys.Select(key => key.KeyId).Distinct(StringComparer.Ordinal).Count() != catalog.Keys.Count)
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "O catálogo de chaves contém IDs vazios ou duplicados.");
        if (catalog.Keys.Any(key => !IsValidKeyId(key.KeyId)
            || key.Algorithm != Algorithm
            || key.Revoked is null
            || string.IsNullOrWhiteSpace(key.PublicKeyPem)
            || key.PublicKeyPem.Length is < 64 or > 2048
            || !key.PublicKeyPem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
            || key.PublicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal)))
            return Result(PackageSignatureStatus.TrustCatalogUnavailable, null,
                "O catálogo contém uma chave incompleta ou fora do formato permitido.");

        PackageSignatureEnvelope? envelope;
        try
        {
            var signatureInfo = new FileInfo(signaturePath);
            if (signatureInfo.Length is <= 0 or > MaximumSignatureBytes)
                return Result(PackageSignatureStatus.MalformedSignature, null,
                    "Arquivo de assinatura vazio ou acima do limite de 16 KiB.");
            envelope = JsonSerializer.Deserialize<PackageSignatureEnvelope>(File.ReadAllBytes(signaturePath), JsonOptions);
        }
        catch (JsonException)
        {
            return Result(PackageSignatureStatus.MalformedSignature, null, "Arquivo de assinatura contém JSON inválido.");
        }
        catch (IOException)
        {
            return Result(PackageSignatureStatus.MalformedSignature, null, "Não foi possível ler o arquivo de assinatura.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result(PackageSignatureStatus.MalformedSignature, null, "A leitura do arquivo de assinatura foi negada.");
        }

        if (envelope is not { SchemaVersion: 1 }
            || !IsValidKeyId(envelope.KeyId)
            || string.IsNullOrWhiteSpace(envelope.SignatureBase64))
            return Result(PackageSignatureStatus.MalformedSignature, envelope?.KeyId,
                "Arquivo de assinatura incompleto ou com versão não suportada.");
        if (!string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal))
            return Result(PackageSignatureStatus.UnsupportedAlgorithm, envelope.KeyId,
                "O algoritmo declarado na assinatura não é aceito.");
        if (envelope.SignatureBase64.Length > MaximumSignatureValueBytes * 2)
            return Result(PackageSignatureStatus.MalformedSignature, envelope.KeyId,
                "A assinatura excede o tamanho máximo permitido.");

        var key = catalog.Keys.SingleOrDefault(item => item.KeyId == envelope.KeyId);
        if (key is null)
            return Result(PackageSignatureStatus.UnknownKey, envelope.KeyId,
                "A assinatura foi criada por uma chave que não está fixada neste aplicativo.");
        if (key.Revoked == true)
            return Result(PackageSignatureStatus.RevokedKey, envelope.KeyId,
                "A chave de publicação está revogada.");
        if (!string.Equals(key.Algorithm, Algorithm, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(key.PublicKeyPem)
            || !key.PublicKeyPem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal))
            return Result(PackageSignatureStatus.InvalidTrustCatalogKey, envelope.KeyId,
                "A chave confiável não corresponde ao formato público ECDSA esperado.");

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(envelope.SignatureBase64);
        }
        catch (FormatException)
        {
            return Result(PackageSignatureStatus.MalformedSignature, envelope.KeyId,
                "A assinatura não está codificada em Base64 válido.");
        }
        if (signature.Length is 0 or > MaximumSignatureValueBytes)
            return Result(PackageSignatureStatus.MalformedSignature, envelope.KeyId,
                "O conteúdo binário da assinatura excede o tamanho esperado.");

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(key.PublicKeyPem);
            if (verifier.KeySize != 256)
                return Result(PackageSignatureStatus.InvalidTrustCatalogKey, envelope.KeyId,
                    "A chave confiável precisa usar curva ECDSA P-256.");

            var valid = verifier.VerifyData(manifestBytes, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            return valid
                ? Result(PackageSignatureStatus.TrustedSignature, envelope.KeyId,
                    "Assinatura do manifesto validada com uma chave confiável do aplicativo.")
                : Result(PackageSignatureStatus.InvalidSignature, envelope.KeyId,
                    "A assinatura não corresponde aos bytes exatos do manifesto.");
        }
        catch (CryptographicException)
        {
            return Result(PackageSignatureStatus.InvalidTrustCatalogKey, envelope.KeyId,
                "A chave pública do catálogo não pôde ser importada ou verificada.");
        }
    }

    private static PackageSignatureVerification Result(PackageSignatureStatus status, string? keyId, string message) =>
        new(status, keyId, message);

    private static bool IsValidKeyId(string? keyId) => keyId is { Length: >= 1 and <= 64 }
        && char.IsAsciiLetterOrDigit(keyId[0])
        && keyId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
