using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using ObsidianInstaller.Core;
using ObsidianInstaller.Windows;

namespace ObsidianInstaller.App;

/// <summary>Plano gravado em disco e entregue ao processo elevado.</summary>
public sealed class InstallPlanDocument
{
    public string PackageId { get; set; } = "";
    public string SystemVersion { get; set; } = "";
    public string Channel { get; set; } = "lab";
    public string ImagePath { get; set; } = "";
    public string ImageSha256 { get; set; } = "";
    public long ImageBytes { get; set; }
    public int DiskNumber { get; set; }
    public string DiskModel { get; set; } = "";
    public string DiskSerial { get; set; } = "";
    public long DiskBytes { get; set; }
    public int DataStartSector { get; set; } = 2097152;
    public string WorkDirectory { get; set; } = "";
    public string LogPath { get; set; } = "";

    /// <summary>Apagar a tabela de partições antes de gravar (padrão: sim).</summary>
    public bool CleanBeforeWrite { get; set; } = true;

    /// <summary>Declarar e formatar a partição de dados depois da gravação.</summary>
    public bool PrepareDataPartition { get; set; } = true;

    /// <summary>Reler a imagem inteira do cartão (lento no console, rápido no leitor).</summary>
    public bool FullReadback { get; set; } = true;

    /// <summary>Disco exposto pelo console via carregador USB (gravação mais lenta).</summary>
    public bool TargetIsConsoleUms { get; set; }
}

public sealed class InstallResultDocument
{
    public bool Success { get; set; }
    public string Stage { get; set; } = "";
    public string Message { get; set; } = "";
    public bool ImageHashVerified { get; set; }
    public bool Cleaned { get; set; }
    public bool LinkChecked { get; set; }
    public bool ReadbackVerified { get; set; }
    public bool LayoutPrepared { get; set; }
    public string? DataRoot { get; set; }
    public double WrittenGiB { get; set; }
    public double Seconds { get; set; }
    public string LogPath { get; set; } = "";
    public List<string> Steps { get; set; } = [];
}

/// <summary>
/// Executa a gravação completa: confere a imagem, apaga o cartão, testa o
/// enlace, grava, relê, declara e formata a partição de dados. Cada etapa
/// registra um passo no log para a interface acompanhar.
/// </summary>
public static class InstallExecutor
{
    private const int LinkCheckBytes = 16 * 1024 * 1024;

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static async Task<InstallResultDocument> RunAsync(
        InstallPlanDocument plan,
        Action<string> log,
        IProgress<WriteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new InstallResultDocument { LogPath = plan.LogPath };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        void Step(string message)
        {
            result.Steps.Add(message);
            log(message);
        }

        void PersistLog()
        {
            if (string.IsNullOrWhiteSpace(plan.LogPath))
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plan.LogPath)!);
                File.AppendAllLines(plan.LogPath, result.Steps);
                result.Steps.Clear();
            }
            catch (IOException)
            {
            }
        }

        try
        {
            if (!File.Exists(plan.ImagePath))
                return Fail(result, "imagem", $"Arquivo de imagem ausente: {plan.ImagePath}");
            var imageInfo = new FileInfo(plan.ImagePath);
            if (imageInfo.Length != plan.ImageBytes)
                return Fail(result, "imagem", $"Tamanho inesperado: {imageInfo.Length:N0} bytes (manifesto declara {plan.ImageBytes:N0}).");

            Step($"Imagem: {Path.GetFileName(plan.ImagePath)} ({plan.ImageBytes:N0} bytes).");
            Step("Conferindo SHA-256 da imagem local antes de gravar...");
            var imageHash = await HashFileAsync(plan.ImagePath, cancellationToken).ConfigureAwait(false);
            PersistLog();
            if (!string.Equals(imageHash, plan.ImageSha256, StringComparison.OrdinalIgnoreCase))
                return Fail(result, "hash", $"O SHA-256 não confere. Calculado: {imageHash}. Nada foi gravado.");
            result.ImageHashVerified = true;
            Step("Hash da imagem confere.");

            Step("Revalidando a identidade do cartão...");
            var inventory = new PhysicalDiskInventory();
            var disks = inventory.Enumerate();
            var target = disks.FirstOrDefault(d => d.Number == plan.DiskNumber);
            if (target is null)
                return Fail(result, "cartão", "O cartão do plano não está mais conectado.");
            if (target.IsSystemDisk)
                return Fail(result, "cartão", "O disco selecionado contém o sistema Windows. Operação recusada.");
            const long sizeTolerance = 64L * 1024 * 1024;
            if (Math.Abs(target.SizeBytes - plan.DiskBytes) > sizeTolerance ||
                !string.Equals(target.Model, plan.DiskModel, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(target.Serial, plan.DiskSerial, StringComparison.OrdinalIgnoreCase))
            {
                return Fail(result, "cartão",
                    $"A identidade do disco {target.Number} mudou desde a conferência. Nada foi gravado.");
            }
            if (target.SizeBytes < plan.ImageBytes + 64L * 1024 * 1024)
                return Fail(result, "cartão", "O cartão é pequeno demais para a imagem e a área de dados mínima.");
            result.Steps.Add($"Cartão confirmado: {target.Identity}.");
            PersistLog();

            using var diskTarget = new PhysicalDiskTarget(target.Number, target.SizeBytes);
            foreach (var message in diskTarget.LockAndDismountVolumes(target.Volumes))
                result.Steps.Add(message);
            PersistLog();

            var work = string.IsNullOrWhiteSpace(plan.WorkDirectory) ? AppContext.BaseDirectory : plan.WorkDirectory;
            var layoutService = new DiskpartLayoutService(work);

            if (plan.CleanBeforeWrite)
            {
                Step("Apagando a tabela de partições do cartão...");
                var clean = layoutService.CleanDisk(target.Number);
                foreach (var line in clean.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (line.Length > 0)
                        result.Steps.Add("diskpart: " + line);
                PersistLog();
                if (!clean.Success)
                    return Fail(result, "limpeza", clean.Message);
                result.Cleaned = true;
                Step("Cartão limpo: nenhuma partição montada.");
            }

            DisableAutoMount();
            // Um único identificador para enlace, gravação e releitura. O corpo da
            // imagem é gravado antes do MBR: sem tabela de partições o Windows não
            // monta volume nenhum e não bloqueia a escrita (defeito observado no leitor).
            await using (var disk = diskTarget.OpenForWrite())
            {
                Step("Testando o enlace de gravação com 16 MiB...");
                var zeros = new byte[LinkCheckBytes];
                disk.Position = 0;
                await disk.WriteAsync(zeros, cancellationToken).ConfigureAwait(false);
                await disk.FlushAsync(cancellationToken).ConfigureAwait(false);
                disk.Position = 0;
                var readBack = new byte[LinkCheckBytes];
                var readTotal = 0;
                while (readTotal < LinkCheckBytes)
                {
                    var n = await disk.ReadAsync(readBack.AsMemory(readTotal, LinkCheckBytes - readTotal), cancellationToken)
                        .ConfigureAwait(false);
                    if (n <= 0)
                        break;
                    readTotal += n;
                }
                if (readTotal != LinkCheckBytes || !readBack.AsSpan(0, LinkCheckBytes).SequenceEqual(zeros))
                    return Fail(result, "enlace", "O teste de gravação não voltou idêntico. O enlace USB pode estar instável; nada foi gravado.");
                result.LinkChecked = true;
                Step("Enlace de gravação respondeu e conferiu.");

                const int mbrLength = MbrTable.SectorSize;
                var bodyBytes = plan.ImageBytes - mbrLength;

                Step("Gravando o sistema no cartão (tabela de partições por último)...");
                var stopwatchWrite = System.Diagnostics.Stopwatch.StartNew();
                var writtenHash = await CopyImageRangeAsync(plan.ImagePath, mbrLength, bodyBytes, disk, mbrLength,
                    progress, cancellationToken).ConfigureAwait(false);
                await disk.FlushAsync(cancellationToken).ConfigureAwait(false);
                var expectedBody = await HashFileRangeAsync(plan.ImagePath, mbrLength, bodyBytes, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(writtenHash, expectedBody, StringComparison.OrdinalIgnoreCase))
                    return Fail(result, "gravação", $"O fluxo gravado divergiu do arquivo (obtido {writtenHash}).");
                result.WrittenGiB = Math.Round(plan.ImageBytes / 1073741824d, 3);
                Step($"Imagem gravada: {plan.ImageBytes:N0} bytes em {stopwatchWrite.Elapsed.TotalSeconds:0.0}s.");
                PersistLog();

                Step(plan.FullReadback
                    ? "Relendo o cartão inteiro para conferir a gravação..."
                    : "Relendo o início e o fim do cartão para conferir a gravação...");
                var readbackHash = plan.FullReadback
                    ? await HashStreamRangeAsync(disk, mbrLength, bodyBytes, cancellationToken).ConfigureAwait(false)
                    : await HashStreamEndsAsync(disk, mbrLength, bodyBytes, cancellationToken).ConfigureAwait(false);
                if (plan.FullReadback
                        ? !string.Equals(readbackHash, expectedBody, StringComparison.OrdinalIgnoreCase)
                        : !string.Equals(readbackHash, await HashFileEndsAsync(plan.ImagePath, mbrLength, bodyBytes, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
                    return Fail(result, "releitura", $"A releitura não corresponde ao esperado (obtido {readbackHash}).");
                result.ReadbackVerified = true;
                Step("Releitura confere com o manifesto.");
                PersistLog();

                Step("Escrevendo a tabela de partições e a área OBSIDIAN...");
                var mbr = new byte[mbrLength];
                await using (var image = new FileStream(plan.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
                {
                    image.Position = 0;
                    var filled = 0;
                    while (filled < mbrLength)
                    {
                        var n = await image.ReadAsync(mbr.AsMemory(filled, mbrLength - filled), cancellationToken).ConfigureAwait(false);
                        if (n <= 0)
                            break;
                        filled += n;
                    }
                }

                if (plan.PrepareDataPartition)
                {
                    var totalSectors = (uint)(plan.DiskBytes / 512);
                    var dataSectors = totalSectors - (uint)plan.DataStartSector;
                    mbr = MbrTable.WithDataPartition(mbr, (uint)plan.DataStartSector, dataSectors);
                    Step($"Partição 3 declarada: início {plan.DataStartSector}, {dataSectors:N0} setores ({dataSectors * 512L / 1073741824d:0.00} GiB).");
                }

                disk.Position = 0;
                await disk.WriteAsync(mbr, cancellationToken).ConfigureAwait(false);
                await disk.FlushAsync(cancellationToken).ConfigureAwait(false);
                disk.Position = 0;
                var sectorZero = new byte[mbrLength];
                disk.ReadExactly(sectorZero, 0, mbrLength);
                if (!sectorZero.AsSpan().SequenceEqual(mbr))
                    return Fail(result, "tabela", "A tabela de partições não voltou idêntica ao ser relida.");
                Step("Tabela de partições gravada e conferida.");
                PersistLog();
            }

            diskTarget.RefreshProperties();

            if (plan.PrepareDataPartition)
            {
                Step("Formatando a área de dados como FAT32 (rótulo OBSIDIAN)...");
                var layout = layoutService.PrepareDataPartition(target.Number, "OBSIDIAN",
                    (uint)plan.DataStartSector, (uint)(plan.DiskBytes / 512 - plan.DataStartSector));
                foreach (var line in layout.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (line.Length > 0)
                        result.Steps.Add("diskpart: " + line);
                PersistLog();
                if (layout.Success && layout.DataRoot is not null)
                {
                    result.LayoutPrepared = true;
                    result.DataRoot = layout.DataRoot;
                    foreach (var folder in new[] { "leitura", "videos", "saves" })
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.Combine(layout.DataRoot, folder));
                        }
                        catch (IOException)
                        {
                        }
                    }
                }
                Step(layout.Message);
            }
            else
            {
                Step("Layout de dados não solicitado: o cartão mantém a tabela de partições da própria imagem.");
            }

            result.Success = result.ReadbackVerified;
            result.Stage = "concluído";
            result.Message = result.LayoutPrepared
                ? "Sistema gravado, conferido e área OBSIDIAN preparada."
                : "Sistema gravado e conferido. A área OBSIDIAN não pôde ser confirmada.";
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Stage = "cancelado";
            result.Message = "Gravação cancelada. O cartão pode ficar incompleto; grave novamente antes de usar.";
            return result;
        }
        catch (Win32Exception ex)
        {
            return Fail(result, "permissão", ex.Message);
        }
        catch (IOException ex)
        {
            return Fail(result, "entrada/saída", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Fail(result, "permissão", ex.Message);
        }
        finally
        {
            EnableAutoMount();
            result.Seconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
            PersistLog();
        }
    }

    /// <summary>Confere só o início e o fim gravados (rápido; usado no enlace do console).</summary>
    private static async Task<(bool Matches, string ActualSha256)> VerifyEndsAsync(
        Stream target, string imagePath, long imageBytes, CancellationToken cancellationToken)
    {
        const int probe = 8 * 1024 * 1024;
        await using var image = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);

        async Task<byte[]> ReadBlockAsync(Stream stream, long position, CancellationToken ct)
        {
            var buffer = new byte[probe];
            stream.Position = position;
            var read = 0;
            while (read < probe)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read, probe - read), ct).ConfigureAwait(false);
                if (n <= 0)
                    break;
                read += n;
            }
            return read == probe ? buffer : buffer[..read];
        }

        var head = await ReadBlockAsync(target, 0, cancellationToken).ConfigureAwait(false);
        var sourceHead = await ReadBlockAsync(image, 0, cancellationToken).ConfigureAwait(false);
        var tail = await ReadBlockAsync(target, imageBytes - probe, cancellationToken).ConfigureAwait(false);
        var sourceTail = await ReadBlockAsync(image, imageBytes - probe, cancellationToken).ConfigureAwait(false);

        using var sha = System.Security.Cryptography.SHA256.Create();
        sha.TransformBlock(head, 0, head.Length, null, 0);
        sha.TransformBlock(tail, 0, tail.Length, null, 0);
        sha.TransformFinalBlock([], 0, 0);
        var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        var matches = head.AsSpan().SequenceEqual(sourceHead) && tail.AsSpan().SequenceEqual(sourceTail);
        return (matches, actual);
    }


    private static void DisableAutoMount() => RunMountvol("/N");

    private static void EnableAutoMount() => RunMountvol("/E");

    private static void RunMountvol(string argument)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "mountvol.exe"),
                Arguments = argument,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10000);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }


    private static async Task<string> HashFileRangeAsync(string path, long offset, long length, CancellationToken cancellationToken)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        stream.Position = offset;
        var buffer = new byte[1 << 20];
        var remaining = length;
        while (remaining > 0)
        {
            var want = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            sha.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static async Task<string> CopyImageRangeAsync(string imagePath, long offset, long length, Stream destination,
        long destinationOffset, IProgress<WriteProgress>? progress, CancellationToken cancellationToken)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var source = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        source.Position = offset;
        destination.Position = destinationOffset;
        var buffer = new byte[4 << 20];
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long done = 0;
        var lastReport = 0L;
        while (done < length)
        {
            var want = (int)Math.Min(buffer.Length, length - done);
            var read = await source.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            sha.TransformBlock(buffer, 0, read, null, 0);
            done += read;
            if (progress is not null && (done - lastReport >= 32L * 1024 * 1024 || done == length))
            {
                lastReport = done;
                var seconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                progress.Report(new WriteProgress(done, length, done / seconds));
            }
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static async Task<string> HashStreamRangeAsync(Stream source, long offset, long length, CancellationToken cancellationToken)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        source.Position = offset;
        var buffer = new byte[1 << 20];
        var remaining = length;
        while (remaining > 0)
        {
            var want = (int)Math.Min(buffer.Length, remaining);
            var read = await source.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            sha.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static Task<string> HashStreamEndsAsync(Stream source, long offset, long length, CancellationToken cancellationToken)
        => HashStreamRangeAsync(source, offset, length, cancellationToken);

    private static Task<string> HashFileEndsAsync(string path, long offset, long length, CancellationToken cancellationToken)
        => HashFileRangeAsync(path, offset, length, cancellationToken);

    private static InstallResultDocument Fail(InstallResultDocument result, string stage, string message)
    {
        result.Success = false;
        result.Stage = stage;
        result.Message = message;
        return result;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        var buffer = new byte[1 << 20];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            sha.TransformBlock(buffer, 0, read, null, 0);
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    public static string WritePlan(InstallPlanDocument plan, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    public static InstallPlanDocument ReadPlan(string path) =>
        JsonSerializer.Deserialize<InstallPlanDocument>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Plano de instalação ilegível.");

    public static void WriteResult(InstallResultDocument result, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static InstallResultDocument ReadResult(string path) =>
        JsonSerializer.Deserialize<InstallResultDocument>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Resultado de instalação ilegível.");
}
