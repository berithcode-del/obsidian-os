using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using ObsidianInstaller.Core;

namespace ObsidianInstaller.Windows;

public sealed record FelState(bool Present, bool DriverReady, string DriverService, string Detail);

/// <summary>
/// Bootstrap do console por USB: detecta o modo FEL, envia o carregador de RAM
/// (u-boot) e espera o cartão aparecer como disco de armazenamento USB.
/// É o caminho usado quando o cartão está no console e não há leitor no PC.
/// </summary>
public sealed class FelBootstrapService(string appDirectory)
{
    private const string FelEnumKey = @"SYSTEM\CurrentControlSet\Enum\USB\VID_1F3A&PID_EFE8";

    public string BootstrapPath => Path.Combine(appDirectory, "assets", "bootstrap", "bootstrap.bin");
    public string FelToolPath => Path.Combine(appDirectory, "tools", "sunxi-fel.exe");

    /// <summary>
    /// Presença do modo FEL (dispositivo vivo agora, não histórico do registro) e
    /// se existe driver ligado a ele.
    /// </summary>
    public FelState Detect()
    {
        UsbDeviceObservation? device;
        try
        {
            device = new UsbDeviceEnumerator().Enumerate()
                .FirstOrDefault(item => item.InstanceId.Contains("VID_1F3A&PID_EFE8", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new FelState(false, false, string.Empty, "Não foi possível consultar os dispositivos USB: " + ex.Message);
        }

        if (device is null)
            return new FelState(false, false, string.Empty, "Nenhum console em modo FEL.");

        var instance = device.InstanceId.Split('\\').LastOrDefault() ?? string.Empty;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\USB\VID_1F3A&PID_EFE8\{instance}");
            var service = key?.GetValue("Service") as string;
            if (!string.IsNullOrWhiteSpace(service))
                return new FelState(true, true, service, $"Console em modo FEL, driver {service}.");
            return new FelState(true, false, string.Empty,
                "O console está em modo FEL, mas o Windows não tem driver WinUSB ligado a ele.");
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new FelState(true, false, string.Empty, "Console em FEL; não foi possível confirmar o driver.");
        }
    }

    /// <summary>Envia o carregador para a RAM do console e aguarda o cartão virar disco.</summary>
    public async Task<(bool Ok, string Output, CardMedia? Disk)> RunAsync(
        IReadOnlyCollection<int> disksBefore,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(FelToolPath))
            return (false, "Ferramenta FEL ausente no pacote (tools\\sunxi-fel.exe).", null);
        if (!File.Exists(BootstrapPath))
            return (false, "Carregador ausente no pacote (assets\\bootstrap\\bootstrap.bin).", null);

        log("Enviando o carregador de RAM para o console…");
        var psi = new ProcessStartInfo
        {
            FileName = FelToolPath,
            Arguments = $"-p uboot \"{BootstrapPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = appDirectory
        };

        var output = new System.Text.StringBuilder();
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                output.AppendLine(e.Data);
                log(e.Data.Trim());
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                output.AppendLine(e.Data);
                log(e.Data.Trim());
            }
        };

        try
        {
            if (!process.Start())
                return (false, "Não foi possível iniciar sunxi-fel.", null);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return (false, output.ToString() + "\nO envio do carregador excedeu 90 segundos.", null);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (false, "Falha ao executar sunxi-fel: " + ex.Message, null);
        }

        if (process.ExitCode != 0)
            return (false, output.ToString() + $"\nsunxi-fel terminou com código {process.ExitCode}.", null);

        log("Carregador enviado. Aguardando o cartão aparecer como disco USB…");
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
            var disks = new PhysicalDiskInventory().Enumerate();
            var fresh = disks.FirstOrDefault(disk =>
                disk.Bus == MediaBusType.Usb && !disksBefore.Contains(disk.Number));
            if (fresh is not null)
                return (true, output.ToString(), fresh);
        }

        return (false, output.ToString() + "\nO carregador rodou, mas nenhum disco novo apareceu em 45 segundos.", null);
    }

    /// <summary>Números de disco observados agora (para detectar o que aparece depois).</summary>
    public static IReadOnlyCollection<int> CurrentDiskNumbers() =>
        new PhysicalDiskInventory().Enumerate().Select(disk => disk.Number).ToHashSet();

    /// <summary>Disco que representa o cartão exposto pelo console (modelo do u-boot).</summary>
    public static bool LooksLikeConsoleDisk(CardMedia disk) =>
        disk.Model.Contains("UMS", StringComparison.OrdinalIgnoreCase) ||
        disk.Model.Contains("Linux", StringComparison.OrdinalIgnoreCase);
}
