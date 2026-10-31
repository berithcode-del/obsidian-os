using System.Security.Cryptography;
using System.Text.Json;

namespace ObsidianInstaller.Core;

public static class ContentKinds
{
    public const string Roms = "roms";
    public const string Leitura = "leitura";
    public const string Videos = "videos";
    public const string Saves = "saves";

    public static readonly string[] All = [Roms, Leitura, Videos, Saves];

    public static string? FromName(string name) =>
        All.FirstOrDefault(kind => string.Equals(kind, name, StringComparison.OrdinalIgnoreCase));

    public static string? FromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".epub" or ".cbz" or ".cbr" or ".pdf" or ".mobi" => Leitura,
        ".mp4" or ".m4v" or ".mov" or ".mkv" or ".avi" => Videos,
        ".sav" or ".srm" or ".state" or ".dsv" or ".rtc" => Saves,
        ".gba" or ".gbc" or ".gb" or ".nes" or ".sfc" or ".smc" or ".nds" or ".cso" or ".iso" or ".zip" or ".chd" or ".pbp" => Roms,
        _ => null
    };
}

public sealed record ContentFileInfo(string Kind, string RelativePath, long Size);

public sealed record ContentItem(string Kind, int FileCount, long TotalBytes)
{
    public string Label => Kind switch
    {
        ContentKinds.Roms => "Jogos (roms)",
        ContentKinds.Leitura => "Leitura (livros)",
        ContentKinds.Videos => "Vídeos",
        ContentKinds.Saves => "Saves",
        _ => Kind
    };

    public string SizeText => TotalBytes >= 1073741824
        ? $"{TotalBytes / 1073741824d:0.00} GiB"
        : $"{TotalBytes / 1048576d:0.0} MiB";
}

/// <summary>Origem de conteúdo: pasta do computador ou imagem FAT32 de backup.</summary>
public interface IContentSource
{
    string DisplayName { get; }
    IReadOnlyList<ContentItem> ScanContent();
    IReadOnlyList<ContentFileInfo> EnumerateFiles(string kind);
    Stream OpenRead(ContentFileInfo file);
}

/// <summary>Backup em pasta no computador, reconhecendo as pastas do console.</summary>
public sealed class FolderContentSource(string root) : IContentSource
{
    public string DisplayName => root;

    public IReadOnlyList<ContentItem> ScanContent()
    {
        var items = new List<ContentItem>();
        foreach (var kind in ContentKinds.All)
        {
            var files = EnumerateFiles(kind);
            if (files.Count > 0)
                items.Add(new ContentItem(kind, files.Count, files.Sum(f => f.Size)));
            else if (Directory.Exists(Path.Combine(root, kind)))
                items.Add(new ContentItem(kind, 0, 0));
        }
        return items;
    }

    public IReadOnlyList<ContentFileInfo> EnumerateFiles(string kind)
    {
        var directory = Directory.Exists(root) ? root : throw new DirectoryNotFoundException(root);
        var kindRoot = Path.Combine(directory, kind);
        var files = new List<ContentFileInfo>();
        if (Directory.Exists(kindRoot))
        {
            foreach (var path in Directory.EnumerateFiles(kindRoot, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(directory, path).Replace('\\', '/');
                files.Add(new ContentFileInfo(kind, rel, new FileInfo(path).Length));
            }
            return files;
        }

        // Arquivos soltos na raiz (ou um diretório plano) também são reconhecidos.
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (ContentKinds.FromExtension(Path.GetExtension(path)) != kind)
                continue;
            if (ContentKinds.FromName(Path.GetDirectoryName(Path.GetRelativePath(directory, path))?.Split(Path.DirectorySeparatorChar).FirstOrDefault() ?? string.Empty) is not null)
                continue;
            var rel = Path.GetRelativePath(directory, path).Replace('\\', '/');
            files.Add(new ContentFileInfo(kind, rel, new FileInfo(path).Length));
        }
        return files;
    }

    public Stream OpenRead(ContentFileInfo file) =>
        File.OpenRead(Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
}

public enum ConflictPolicy
{
    Skip,
    Replace,
    KeepBoth
}

public sealed record RestoreOptions(
    string DestinationRoot,
    IReadOnlyCollection<string> Kinds,
    ConflictPolicy Policy,
    bool VerifyHashes = true)
{
    public bool Includes(string kind) => Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase);
}

public sealed record RestoreProgress(string CurrentFile, int FilesDone, int FilesTotal, long BytesDone, long BytesTotal)
{
    public double Percent => BytesTotal <= 0 ? (FilesTotal == 0 ? 0 : FilesDone * 100d / FilesTotal) : BytesDone * 100d / BytesTotal;
}

public sealed record RestoreOutcome(
    int Copied,
    int Skipped,
    int Replaced,
    int Renamed,
    int Failed,
    long BytesCopied,
    string Destination,
    IReadOnlyList<string> Messages,
    string ReportPath);

/// <summary>
/// Cópia real do conteúdo para o destino, com checagem de espaço, tratamento de
/// conflito, verificação de hash por arquivo e relatório em JSON.
/// </summary>
public sealed class ContentRestorer(string reportDirectory)
{
    public long FreeBytesOf(string destinationRoot)
    {
        var path = Path.GetPathRoot(Path.GetFullPath(destinationRoot));
        if (string.IsNullOrEmpty(path))
            return 0;
        return new DriveInfo(path).AvailableFreeSpace;
    }

    public async Task<RestoreOutcome> RestoreAsync(
        IContentSource source,
        RestoreOptions options,
        IProgress<RestoreProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.DestinationRoot))
            throw new DirectoryNotFoundException(options.DestinationRoot);

        var files = options.Kinds
            .SelectMany(kind => source.EnumerateFiles(kind))
            .Where(f => f.Size >= 0)
            .ToArray();
        var totalBytes = files.Sum(f => f.Size);

        if (files.Length == 0)
            throw new InvalidOperationException("Nenhum arquivo foi selecionado para restaurar.");

        var free = FreeBytesOf(options.DestinationRoot);
        if (free > 0 && totalBytes > free)
            throw new InvalidOperationException(
                $"Espaço insuficiente no destino: são necessários {totalBytes:N0} bytes e há {free:N0} disponíveis.");

        var messages = new List<string>();
        int copied = 0, skipped = 0, replaced = 0, renamed = 0, failed = 0;
        long written = 0;
        var done = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetRelative = FatNames.SanitizeRelative(MapDestination(options, file));
            if (!string.Equals(targetRelative, file.RelativePath, StringComparison.Ordinal))
                messages.Add($"Destino ajustado: {file.RelativePath} → {targetRelative}");

            var targetPath = Path.Combine(options.DestinationRoot, targetRelative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (File.Exists(targetPath))
            {
                switch (options.Policy)
                {
                    case ConflictPolicy.Skip:
                        skipped++;
                        done++;
                        messages.Add($"Mantido (já existia): {targetRelative}");
                        continue;
                    case ConflictPolicy.Replace:
                        replaced++;
                        break;
                    case ConflictPolicy.KeepBoth:
                        targetPath = NextFreeName(targetPath, ref renamed);
                        messages.Add($"Gravado com novo nome: {Path.GetFileName(targetPath)}");
                        break;
                }
            }

            try
            {
                var (sha, bytes) = await CopyVerifiedAsync(source, file, targetPath, cancellationToken).ConfigureAwait(false);
                if (options.VerifyHashes)
                {
                    var check = await HashFileAsync(targetPath, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(check, sha, StringComparison.OrdinalIgnoreCase))
                    {
                        failed++;
                        messages.Add($"Falha de verificação em {targetRelative}: hash não confere.");
                        done++;
                        continue;
                    }
                }
                copied++;
                written += bytes;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
                messages.Add($"Falha ao copiar {targetRelative}: {ex.Message}");
            }

            done++;
            progress?.Report(new RestoreProgress(file.RelativePath, done, files.Length, written, totalBytes));
        }

        var report = new
        {
            product = "Obsidian Installer",
            timestamp_utc = DateTime.UtcNow.ToString("O"),
            source = source.DisplayName,
            destination = options.DestinationRoot,
            kinds = options.Kinds,
            policy = options.Policy.ToString(),
            files_planned = files.Length,
            bytes_planned = totalBytes,
            copied,
            replaced,
            renamed,
            skipped,
            failed,
            bytes_copied = written,
            free_bytes_after = FreeBytesOf(options.DestinationRoot),
            messages
        };
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, $"restauracao-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

        return new RestoreOutcome(copied, skipped, replaced, renamed, failed, written,
            options.DestinationRoot, messages, reportPath);
    }

    private static async Task<(string Sha256, long Bytes)> CopyVerifiedAsync(
        IContentSource source, ContentFileInfo file, string targetPath, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        await using var input = source.OpenRead(file);
        await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
        var buffer = new byte[1 << 20];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
        }
        sha.TransformFinalBlock([], 0, 0);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        return (Convert.ToHexString(sha.Hash!).ToLowerInvariant(), total);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            sha.TransformBlock(buffer, 0, read, null, 0);
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    /// <summary>
    /// Saves restaurados para a home do sistema vão para a pasta que o RetroArch
    /// lê; nos demais destinos seguem a estrutura roms/leitura/videos/saves.
    /// </summary>
    private static string MapDestination(RestoreOptions options, ContentFileInfo file)
    {
        var looksLikeSystemHome = File.Exists(Path.Combine(options.DestinationRoot, "gameman-launcher"));
        if (file.Kind == ContentKinds.Saves && looksLikeSystemHome)
        {
            var prefix = ContentKinds.Saves + "/";
            var rest = file.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? file.RelativePath[prefix.Length..]
                : Path.GetFileName(file.RelativePath);
            return $".config/retroarch/saves/{rest}";
        }
        return file.RelativePath;
    }

    private static string NextFreeName(string path, ref int renamed)
    {
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{name}-{i}{extension}");
            if (!File.Exists(candidate))
            {
                renamed++;
                return candidate;
            }
        }
        throw new IOException($"Não foi possível gerar um nome livre para {path}.");
    }
}

/// <summary>Nomes válidos em cartões FAT32.</summary>
public static class FatNames
{
    private static readonly char[] Invalid = ['"', '*', '?', '<', '>', '|', ':', '\\', '/', '\0'];

    public static string SanitizeRelative(string relative)
    {
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', parts.Select(SanitizeName));
    }

    public static string SanitizeName(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(Invalid.Contains(c) || c < 32 ? '-' : c);
        var cleaned = builder.ToString().TrimEnd(' ', '.');
        return cleaned.Length == 0 ? "_" : cleaned;
    }
}
