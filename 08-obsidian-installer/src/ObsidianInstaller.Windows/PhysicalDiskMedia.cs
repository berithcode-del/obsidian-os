using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ObsidianInstaller.Core;

namespace ObsidianInstaller.Windows;

/// <summary>
/// Inventário de discos e volumes. Usa WMI porque o Windows nega a abertura de
/// \\.\PhysicalDriveN para quem não é administrador, e a interface precisa
/// listar cartões antes da elevação. Nenhuma escrita acontece aqui.
/// </summary>
public sealed class PhysicalDiskInventory
{
    public IReadOnlyList<CardMedia> Enumerate()
    {
        var systemDisk = TryGetSystemDiskNumber();
        var disks = new List<CardMedia>();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        foreach (ManagementObject drive in searcher.Get().Cast<ManagementObject>())
        {
            using (drive)
            {
                int number;
                try
                {
                    number = Convert.ToInt32(drive["Index"]);
                }
                catch (Exception)
                {
                    continue;
                }

                var model = (drive["Model"] as string)?.Trim();
                if (string.IsNullOrWhiteSpace(model))
                    model = $"Disco {number}";
                var pnpId = (drive["PNPDeviceID"] as string) ?? string.Empty;
                var serial = (drive["SerialNumber"] as string)?.Trim();
                if (string.IsNullOrWhiteSpace(serial))
                    serial = pnpId.Split('\\').LastOrDefault() ?? "não informada";
                long size = 0;
                try
                {
                    size = Convert.ToInt64(drive["Size"] ?? 0L);
                }
                catch (Exception)
                {
                }

                var interfaceType = (drive["InterfaceType"] as string) ?? string.Empty;
                var mediaType = (drive["MediaType"] as string) ?? string.Empty;
                var bus = interfaceType.Equals("USB", StringComparison.OrdinalIgnoreCase)
                    ? MediaBusType.Usb
                    : mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase)
                        ? MediaBusType.Sd
                        : MediaBusType.Other;

                disks.Add(new CardMedia(
                    number,
                    model,
                    serial,
                    bus,
                    size,
                    number == systemDisk,
                    $@"\\.\PhysicalDrive{number}",
                    CollectVolumes(drive)));
            }
        }

        return disks.OrderBy(disk => disk.Number).ToArray();
    }

    private static IReadOnlyList<CardVolume> CollectVolumes(ManagementObject disk)
    {
        var volumes = new List<CardVolume>();
        try
        {
            foreach (ManagementObject partition in disk.GetRelated("Win32_DiskPartition").Cast<ManagementObject>())
            {
                using (partition)
                {
                    foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>())
                    {
                        using (logical)
                        {
                            var deviceId = logical["DeviceID"] as string;
                            if (string.IsNullOrWhiteSpace(deviceId))
                                continue;
                            long size = 0, free = 0;
                            try
                            {
                                size = Convert.ToInt64(logical["Size"] ?? 0L);
                                free = Convert.ToInt64(logical["FreeSpace"] ?? 0L);
                            }
                            catch (Exception)
                            {
                            }

                            var label = (logical["VolumeName"] as string) ?? deviceId.TrimEnd(':');
                            var fileSystem = (logical["FileSystem"] as string) ?? string.Empty;
                            var driveType = 0;
                            try
                            {
                                driveType = Convert.ToInt32(logical["DriveType"] ?? 0);
                            }
                            catch (Exception)
                            {
                            }

                            volumes.Add(new CardVolume(
                                deviceId + "\\",
                                label,
                                fileSystem,
                                size,
                                free,
                                driveType == 2));
                        }
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }
        return volumes;
    }

    private static int? TryGetSystemDiskNumber()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\');
            if (string.IsNullOrWhiteSpace(root))
                return null;
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_LogicalDisk WHERE DeviceID='{root}'");
            foreach (ManagementObject logical in searcher.Get().Cast<ManagementObject>())
            {
                using (logical)
                {
                    foreach (ManagementObject partition in logical.GetRelated("Win32_DiskPartition").Cast<ManagementObject>())
                    {
                        using (partition)
                        {
                            foreach (ManagementObject drive in partition.GetRelated("Win32_DiskDrive").Cast<ManagementObject>())
                            {
                                using (drive)
                                {
                                    return Convert.ToInt32(drive["Index"]);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException)
        {
        }
        return null;
    }

    internal static class Native
    {
        internal const uint GenericRead = 0x80000000;
        internal const uint GenericWrite = 0x40000000;
        internal const uint FileShareRead = 0x00000001;
        internal const uint FileShareWrite = 0x00000002;
        internal const uint OpenExisting = 3;
        internal const uint IoctlDiskUpdateProperties = 0x00070148;
        internal const uint FsctlLockVolume = 0x00090018;
        internal const uint FsctlDismountVolume = 0x00090020;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, IntPtr inBuffer,
            int inBufferSize, byte[] outBuffer, int outBufferSize, out int bytesReturned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FlushFileBuffers(SafeFileHandle handle);
    }
}

/// <summary>
/// Acesso ao cartão físico para escrita: bloqueia/desmonta volumes, abre o disco
/// com acesso bruto e pede ao Windows para reler a tabela de partições.
/// </summary>
public sealed class PhysicalDiskTarget(int diskNumber, long sizeBytes) : IDisposable
{
    private readonly List<SafeFileHandle> _lockedVolumes = [];

    public long SizeBytes { get; } = sizeBytes;

    /// <summary>Abre o disco para escrita bruta. O fluxo devolvido pertence a quem chamou.</summary>
    public Stream OpenForWrite()
    {
        var handle = PhysicalDiskInventory.Native.CreateFile($@"\\.\PhysicalDrive{diskNumber}",
            PhysicalDiskInventory.Native.GenericRead | PhysicalDiskInventory.Native.GenericWrite,
            PhysicalDiskInventory.Native.FileShareRead | PhysicalDiskInventory.Native.FileShareWrite,
            IntPtr.Zero, PhysicalDiskInventory.Native.OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, error == 5
                ? "O Windows negou acesso ao cartão. Execute o instalador como administrador."
                : "Não foi possível abrir o cartão para gravação.");
        }
        return new FileStream(handle, FileAccess.ReadWrite, 1 << 20, isAsync: false);
    }

    /// <summary>
    /// Bloqueia e desmonta os volumes do cartão para permitir escrita exclusiva.
    /// Tenta várias vezes e, se o volume resistir, remove o ponto de montagem
    /// com mountvol /p (força o Windows a soltar os identificadores).
    /// </summary>
    public IReadOnlyList<string> LockAndDismountVolumes(IEnumerable<CardVolume> volumes)
    {
        var messages = new List<string>();
        foreach (var volume in volumes)
        {
            var root = volume.Root.TrimEnd('\\');
            var locked = false;
            for (var attempt = 1; attempt <= 4 && !locked; attempt++)
            {
                var handle = PhysicalDiskInventory.Native.CreateFile($@"\\.\\{root}",
                    PhysicalDiskInventory.Native.GenericRead | PhysicalDiskInventory.Native.GenericWrite,
                    PhysicalDiskInventory.Native.FileShareRead | PhysicalDiskInventory.Native.FileShareWrite,
                    IntPtr.Zero, PhysicalDiskInventory.Native.OpenExisting, 0, IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    messages.Add($"Volume {root}: não foi possível abrir para bloqueio (tentativa {attempt}).");
                    handle.Dispose();
                    Thread.Sleep(400);
                    continue;
                }
                var ok = PhysicalDiskInventory.Native.DeviceIoControl(handle, PhysicalDiskInventory.Native.FsctlLockVolume,
                    IntPtr.Zero, 0, [], 0, out _, IntPtr.Zero);
                var dismounted = ok && PhysicalDiskInventory.Native.DeviceIoControl(handle,
                    PhysicalDiskInventory.Native.FsctlDismountVolume, IntPtr.Zero, 0, [], 0, out _, IntPtr.Zero);
                if (ok && dismounted)
                {
                    _lockedVolumes.Add(handle);
                    locked = true;
                    messages.Add($"Volume {root}: bloqueado e desmontado.");
                    break;
                }
                handle.Dispose();
                messages.Add($"Volume {root}: bloqueio não confirmado (tentativa {attempt}).");
                Thread.Sleep(400);
            }

            if (!locked)
            {
                // Força: remove o ponto de montagem; o volume deixa de estar em uso.
                var removed = RunMountvol(root);
                messages.Add(removed
                    ? $"Volume {root}: ponto de montagem removido (mountvol /p)."
                    : $"Volume {root}: mountvol /p falhou.");
                Thread.Sleep(600);
                var handle = PhysicalDiskInventory.Native.CreateFile($@"\\.\\{root}",
                    PhysicalDiskInventory.Native.GenericRead | PhysicalDiskInventory.Native.GenericWrite,
                    PhysicalDiskInventory.Native.FileShareRead | PhysicalDiskInventory.Native.FileShareWrite,
                    IntPtr.Zero, PhysicalDiskInventory.Native.OpenExisting, 0, IntPtr.Zero);
                if (!handle.IsInvalid)
                {
                    var ok = PhysicalDiskInventory.Native.DeviceIoControl(handle, PhysicalDiskInventory.Native.FsctlLockVolume,
                        IntPtr.Zero, 0, [], 0, out _, IntPtr.Zero);
                    if (ok)
                    {
                        _lockedVolumes.Add(handle);
                        locked = true;
                        messages.Add($"Volume {root}: bloqueado após mountvol.");
                    }
                    else
                    {
                        handle.Dispose();
                    }
                }
            }
        }
        return messages;
    }

    private static bool RunMountvol(string root)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "mountvol.exe"),
                Arguments = $"\"{root}\" /p",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is null)
                return false;
            process.WaitForExit(15000);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Pede ao Windows para reler a tabela de partições do cartão.</summary>
    public void RefreshProperties()
    {
        using var handle = PhysicalDiskInventory.Native.CreateFile($@"\\.\PhysicalDrive{diskNumber}",
            PhysicalDiskInventory.Native.GenericRead,
            PhysicalDiskInventory.Native.FileShareRead | PhysicalDiskInventory.Native.FileShareWrite,
            IntPtr.Zero, PhysicalDiskInventory.Native.OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            return;
        PhysicalDiskInventory.Native.DeviceIoControl(handle, PhysicalDiskInventory.Native.IoctlDiskUpdateProperties,
            IntPtr.Zero, 0, [], 0, out _, IntPtr.Zero);
        PhysicalDiskInventory.Native.FlushFileBuffers(handle);
    }

    public void Dispose()
    {
        foreach (var volume in _lockedVolumes)
            volume.Dispose();
    }
}

/// <summary>Formata e rotula a partição de dados com o diskpart do Windows.</summary>
public sealed class DiskpartLayoutService(string workDirectory)
{
    public sealed record LayoutResult(bool Success, string Output, string? DataRoot, string Message);

    public sealed record CleanResult(bool Success, string Output, string Message);

    /// <summary>Apaga a tabela de partições e as assinaturas do disco (diskpart clean).</summary>
    public CleanResult CleanDisk(int diskNumber)
    {
        Directory.CreateDirectory(workDirectory);
        var scriptPath = Path.Combine(workDirectory, $"limpeza-{diskNumber}-{DateTime.Now:HHmmss}.txt");
        File.WriteAllText(scriptPath, $"select disk {diskNumber}{Environment.NewLine}clean{Environment.NewLine}exit{Environment.NewLine}");
        var output = RunDiskpart(scriptPath);
        return Classify(output, "limpeza do cartão");
    }


    public LayoutResult PrepareDataPartition(int diskNumber, string label, uint startSector, uint sectorCount)
    {
        Directory.CreateDirectory(workDirectory);
        var scriptPath = Path.Combine(workDirectory, $"layout-{diskNumber}-{DateTime.Now:HHmmss}.txt");
        var script = string.Join(Environment.NewLine,
        [
            "rescan",
            $"select disk {diskNumber}",
            "select partition 3",
            $"format fs=fat32 quick label={label}",
            "assign"
        ]);
        File.WriteAllText(scriptPath, script + Environment.NewLine);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "diskpart.exe"),
            Arguments = $"/s \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o diskpart.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(180000);

        var lowered = output.ToLowerInvariant();
        var failed = lowered.Contains("erro") || lowered.Contains("error") || lowered.Contains("não foi possível")
                     || lowered.Contains("falha") || lowered.Contains("invalid");
        if (failed || process.ExitCode != 0)
            return new LayoutResult(false, output, null,
                "O Windows não confirmou a formatação da partição de dados. O cartão permanece com a imagem gravada, mas sem área OBSIDIAN.");

        var dataRoot = WaitForVolumeLabel(label, TimeSpan.FromSeconds(20));
        return dataRoot is null
            ? new LayoutResult(false, output, null,
                "A partição foi formatada, mas o volume ainda não apareceu para o Windows. Desconecte e reconecte o cartão para vê-lo.")
            : new LayoutResult(true, output, dataRoot, $"Área de dados criada em {dataRoot}.");
    }

    private static string RunDiskpart(string scriptPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "diskpart.exe"),
            Arguments = $"/s \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o diskpart.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(180000);
        return output;
    }

    private static CleanResult Classify(string output, string what)
    {
        var lowered = output.ToLowerInvariant();
        var failed = lowered.Contains("erro") || lowered.Contains("error") || lowered.Contains("não foi possível")
                     || lowered.Contains("falha") || lowered.Contains("invalid");
        return failed
            ? new CleanResult(false, output, $"O Windows não confirmou a {what}.")
            : new CleanResult(true, output, $"Confirmado: {what}.");
    }

    private static string? WaitForVolumeLabel(string label, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && string.Equals(drive.VolumeLabel, label, StringComparison.OrdinalIgnoreCase))
                        return drive.Name;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            Thread.Sleep(700);
        }
        return null;
    }
}
