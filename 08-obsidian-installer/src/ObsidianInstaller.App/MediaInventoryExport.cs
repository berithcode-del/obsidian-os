using System.IO;
using System.Text.Json;

namespace ObsidianInstaller.App;

/// <summary>Modo diagnóstico: exporta o inventário de mídia em JSON, sem alterar nada.</summary>
public static class MediaInventoryExport
{
    public sealed record MediaEntry(
        int Number,
        string Model,
        string Serial,
        string Bus,
        long Bytes,
        bool IsSystemDisk,
        string DevicePath,
        IReadOnlyList<VolumeEntry> Volumes);

    public sealed record VolumeEntry(string Root, string Label, string FileSystem, long Bytes, long Free, bool Removable);

    public static int Run(string outputPath)
    {
        var disks = new ObsidianInstaller.Windows.PhysicalDiskInventory().Enumerate();
        var payload = new
        {
            generated_utc = DateTime.UtcNow.ToString("O"),
            elevated = InstallExecutor.IsElevated(),
            disks = disks.Select(disk => new MediaEntry(
                disk.Number,
                disk.Model,
                disk.Serial,
                disk.BusLabel,
                disk.SizeBytes,
                disk.IsSystemDisk,
                disk.DevicePath,
                disk.Volumes.Select(v => new VolumeEntry(v.Root, v.Label, v.FileSystem, v.SizeBytes, v.FreeBytes, v.IsRemovable)).ToArray()))
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
