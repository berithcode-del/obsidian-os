using System.Security.Cryptography;

namespace ObsidianInstaller.Core;

public enum MediaBusType
{
    Unknown,
    Usb,
    Sd,
    Other
}

/// <summary>Um volume montado que pertence a um cartão.</summary>
public sealed record CardVolume(string Root, string Label, string FileSystem, long SizeBytes, long FreeBytes, bool IsRemovable);

/// <summary>Um cartão/discos físico observado no Windows. Somente metadados.</summary>
public sealed record CardMedia(
    int Number,
    string Model,
    string Serial,
    MediaBusType Bus,
    long SizeBytes,
    bool IsSystemDisk,
    string DevicePath,
    IReadOnlyList<CardVolume> Volumes)
{
    public double SizeGiB => Math.Round(SizeBytes / 1073741824d, 2);

    public string BusLabel => Bus switch
    {
        MediaBusType.Usb => "USB",
        MediaBusType.Sd => "SD",
        MediaBusType.Other => "outro",
        _ => "desconhecido"
    };

    /// <summary>Identificação curta e estável para mostrar ao cliente antes de apagar.</summary>
    public string Identity => $"Disco {Number} · {Model} · {SizeGiB:0.00} GiB · série {Serial}";

    public IReadOnlyList<CardVolume> RemovableVolumes => Volumes.Where(v => v.IsRemovable).ToArray();
}

/// <summary>Plano imutável de gravação; é o que o helper elevado recebe e revalida.</summary>
public sealed record ImageWritePlan(
    string PackageId,
    string SystemVersion,
    string Channel,
    string ImagePath,
    string ExpectedImageSha256,
    long ImageBytes,
    int DiskNumber,
    string DiskModel,
    string DiskSerial,
    long DiskBytes,
    int DataStartSector,
    string ProductDirectory)
{
    public uint DataSectorCount => DiskBytes <= 0 ? 0
        : (uint)Math.Max(0, (DiskBytes / 512) - DataStartSector);

    public bool IsLabChannel => string.Equals(Channel, "lab", StringComparison.OrdinalIgnoreCase);

    public string TargetIdentity => $"Disco {DiskNumber} · {DiskModel} · {DiskBytes / 1073741824d:0.00} GiB · série {DiskSerial}";
}

public sealed record WriteProgress(long DoneBytes, long TotalBytes, double BytesPerSecond)
{
    public double Percent => TotalBytes <= 0 ? 0 : Math.Min(100d, DoneBytes * 100d / TotalBytes);
}

public sealed record ImageWriteResult(string Sha256, long Bytes, TimeSpan Elapsed);

/// <summary>
/// Escrita bruta de imagem em qualquer fluxo posicionável (arquivo de teste ou
/// dispositivo físico). O chamador monta o fluxo; esta classe só copia, mede,
/// calcula hash e relê para conferir.
/// </summary>
public static class ImageWriter
{
    private const int BufferSize = 4 * 1024 * 1024;

    public static async Task<ImageWriteResult> WriteAsync(
        Stream source,
        Stream target,
        long imageBytes,
        IProgress<WriteProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        target.Position = 0;
        var buffer = new byte[BufferSize];
        long done = 0;
        var started = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long lastReport = 0;

        while (done < imageBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(buffer.Length, imageBytes - done);
            var read = await source.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                throw new IOException($"A imagem terminou em {done} bytes, antes dos {imageBytes} declarados.");
            sha.TransformBlock(buffer, 0, read, null, 0);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            done += read;
            if (progress is not null && (stopwatch.ElapsedMilliseconds - lastReport > 400 || done == imageBytes))
            {
                lastReport = stopwatch.ElapsedMilliseconds;
                var seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                progress.Report(new WriteProgress(done, imageBytes, done / seconds));
            }
        }

        if (source.ReadByte() >= 0)
            throw new IOException("A imagem é maior do que o tamanho declarado no manifesto.");
        sha.TransformFinalBlock([], 0, 0);
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        return new ImageWriteResult(Convert.ToHexString(sha.Hash!).ToLowerInvariant(), done,
            DateTime.UtcNow - started);
    }

    /// <summary>Relê os primeiros <paramref name="imageBytes"/> e confere o SHA-256.</summary>
    public static async Task<(bool Matches, string ActualSha256)> VerifyAsync(
        Stream target,
        long imageBytes,
        string expectedSha256,
        IProgress<WriteProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        target.Position = 0;
        var buffer = new byte[BufferSize];
        long done = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long lastReport = 0;
        while (done < imageBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(buffer.Length, imageBytes - done);
            var read = await target.ReadAsync(buffer.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;
            sha.TransformBlock(buffer, 0, read, null, 0);
            done += read;
            if (progress is not null && (stopwatch.ElapsedMilliseconds - lastReport > 400 || done == imageBytes))
            {
                lastReport = stopwatch.ElapsedMilliseconds;
                var seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                progress.Report(new WriteProgress(done, imageBytes, done / seconds));
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        return (done == imageBytes && string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase), actual);
    }
}

/// <summary>Entrada da tabela de partições MBR (setores de 512 bytes).</summary>
public readonly record struct MbrPartition(int Index, byte BootFlag, byte Type, uint StartSector, uint SectorCount)
{
    public long StartByte => StartSector * 512L;
    public long ByteLength => SectorCount * 512L;
    public long EndSector => StartSector + (long)SectorCount;

    public string TypeLabel => Type switch
    {
        0x83 => "ext4 (Linux)",
        0x0B or 0x0C => "FAT32",
        0x06 or 0x0E => "FAT16",
        0x07 => "NTFS/exFAT",
        0xEE => "GPT protetor",
        _ => $"tipo 0x{Type:X2}"
    };
}

/// <summary>Leitura e alteração mínima da MBR do cartão de sistema.</summary>
public static class MbrTable
{
    public const int SectorSize = 512;
    public const int PartitionEntryOffset = 446;
    public const int PartitionEntrySize = 16;

    public static IReadOnlyList<MbrPartition> Parse(ReadOnlySpan<byte> mbr)
    {
        if (mbr.Length < SectorSize)
            throw new ArgumentException("A MBR precisa de pelo menos 512 bytes.", nameof(mbr));
        var list = new List<MbrPartition>(4);
        for (var i = 0; i < 4; i++)
        {
            var offset = PartitionEntryOffset + i * PartitionEntrySize;
            var type = mbr[offset + 4];
            if (type == 0)
                continue;
            var start = BitConverter.ToUInt32(mbr.Slice(offset + 8, 4));
            var count = BitConverter.ToUInt32(mbr.Slice(offset + 12, 4));
            list.Add(new MbrPartition(i + 1, mbr[offset], type, start, count));
        }
        return list;
    }

    /// <summary>
    /// Devolve uma cópia do setor 0 com a partição de dados (slot 3) apontando do
    /// início informado até o fim do cartão. Não formata: apenas declara a região.
    /// </summary>
    public static byte[] WithDataPartition(ReadOnlySpan<byte> mbr, uint startSector, uint sectorCount, int slot = 3)
    {
        if (slot is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (sectorCount == 0)
            throw new ArgumentException("A partição de dados precisa de ao menos um setor.", nameof(sectorCount));

        var copy = new byte[SectorSize];
        for (var i = 0; i < SectorSize && i < mbr.Length; i++)
            copy[i] = mbr[i];
        copy[510] = 0x55;
        copy[511] = 0xAA;

        var offset = PartitionEntryOffset + (slot - 1) * PartitionEntrySize;
        copy[offset] = 0x00;                       // não inicializável
        copy[offset + 1] = 0xFE;                   // CHS inicial em modo LBA
        copy[offset + 2] = 0xFF;
        copy[offset + 3] = 0xFF;
        copy[offset + 4] = 0x0C;                   // FAT32 LBA
        copy[offset + 5] = 0xFE;                   // CHS final em modo LBA
        copy[offset + 6] = 0xFF;
        copy[offset + 7] = 0xFF;
        BitConverter.GetBytes(startSector).CopyTo(copy, offset + 8);
        BitConverter.GetBytes(sectorCount).CopyTo(copy, offset + 12);
        return copy;
    }
}
