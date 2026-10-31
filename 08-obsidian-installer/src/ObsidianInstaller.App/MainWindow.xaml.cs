using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ObsidianInstaller.Core;
using ObsidianInstaller.Windows;

namespace ObsidianInstaller.App;

public partial class MainWindow : Window
{
    private sealed record Package(string ManifestPath, string ImagePath, string DisplayName, string Version,
        string Sha256, long Bytes);

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly FelBootstrapService _fel = new(AppContext.BaseDirectory);
    private readonly ObservableCollection<CardMedia> _candidates = [];
    private readonly string _workDirectory = Path.Combine(AppContext.BaseDirectory, "work");
    private Package? _package;
    private bool _packageVerified;
    private CardMedia? _target;
    private bool _bootstrapRunning;
    private bool _writeRunning;
    private CancellationTokenSource? _writeCts;
    private int _step = 1;
    private IContentSource? _contentSource;

    public MainWindow()
    {
        InitializeComponent();
        TargetList.ItemsSource = _candidates;
        Loaded += (_, _) =>
        {
            _ = LoadPackageAsync();
            _poll.Tick += async (_, _) => await PollAsync();
            _poll.Start();
            SetStep(1);
            var logPath = Environment.GetEnvironmentVariable("OBSIDIAN_UI_LOG");
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var lines = new List<string>
                    {
                        $"window ActualWidth={ActualWidth:0.0} ActualHeight={ActualHeight:0.0}",
                        $"ContentScroller ActualWidth={ContentScroller.ActualWidth:0.0} ViewportWidth={ContentScroller.ViewportWidth:0.0} ExtentWidth={ContentScroller.ExtentWidth:0.0}",
                        $"ContentGrid ActualWidth={ContentGrid.ActualWidth:0.0} DesiredSize={ContentGrid.DesiredSize.Width:0.0}",
                        $"PageConnect ActualWidth={PageConnect.ActualWidth:0.0} DesiredSize={PageConnect.DesiredSize.Width:0.0}",
                        $"systemDpi scale={PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1:0.00}",
                        $"workArea {SystemParameters.WorkArea.Width:0.0}x{SystemParameters.WorkArea.Height:0.0}",
                        $"primaryScreen {SystemParameters.PrimaryScreenWidth:0.0}x{SystemParameters.PrimaryScreenHeight:0.0}"
                    };
                    File.AppendAllLines(logPath, lines);
                }), DispatcherPriority.ApplicationIdle);
            }
        };
        Closing += (_, _) => (_contentSource as IDisposable)?.Dispose();
    }

    // ------------------------------------------------------------------ pacote
    private async Task LoadPackageAsync()
    {
        try
        {
            var imageDir = Path.Combine(AppContext.BaseDirectory, "image");
            if (!Directory.Exists(imageDir))
            {
                PackageValue.Text = "Pacote não encontrado";
                PackageDetail.Text = $"Coloque a imagem e o manifesto em {imageDir}";
                PackageDot.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x3C));
                return;
            }

            var manifestPath = Directory.EnumerateFiles(imageDir, "*.manifest.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            var imagePath = Directory.EnumerateFiles(imageDir, "*.img").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (manifestPath is null || imagePath is null)
            {
                PackageValue.Text = "Pacote incompleto";
                PackageDetail.Text = "Precisa de um .img e de um .manifest.json na pasta image";
                return;
            }

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var root = doc.RootElement;
            var payload = root.GetProperty("payload");
            _package = new Package(
                manifestPath,
                imagePath,
                root.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "Obsidian OS",
                root.TryGetProperty("system_version", out var sv) ? sv.GetString() ?? "" : "",
                payload.GetProperty("sha256").GetString() ?? "",
                payload.GetProperty("image_bytes").GetInt64());

            PackageValue.Text = $"Obsidian OS {_package.Version}";
            PackageDetail.Text = $"Conferindo {Path.GetFileName(imagePath)}…";
            VersionChip.Text = $"v{_package.Version} · CANAL LAB";
            FooterLeft.Text = $"{Path.GetFileName(imagePath)} · verificando…";

            var hash = await Task.Run(() => HashFile(imagePath));
            _packageVerified = hash == _package.Sha256 && new FileInfo(imagePath).Length == _package.Bytes;
            PackageDetail.Text = _packageVerified
                ? $"{_package.Bytes / 1048576} MiB · SHA-256 {_package.Sha256[..16]}… conferido"
                : "O hash do arquivo não confere com o manifesto";
            PackageDot.Foreground = _packageVerified
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x5A, 0x3C));
            FooterLeft.Text = _packageVerified
                ? $"{Path.GetFileName(imagePath)} · {_package.Sha256[..16]}…"
                : "pacote com problema";
            UpdatePrimary();
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            PackageValue.Text = "Pacote ilegível";
            PackageDetail.Text = ex.Message;
        }
    }

    private static string HashFile(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(path);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            sha.TransformBlock(buffer, 0, read, null, 0);
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    // ------------------------------------------------------------------ descoberta
    private async Task PollAsync()
    {
        if (_writeRunning || _step != 1)
            return;
        try
        {
            var disks = await Task.Run(() => new PhysicalDiskInventory().Enumerate());
            var fel = await Task.Run(_fel.Detect);

            var found = disks
                .Where(d => !d.IsSystemDisk && d.Bus == MediaBusType.Usb && d.SizeBytes > 1024L * 1024 * 1024)
                .OrderBy(d => FelBootstrapService.LooksLikeConsoleDisk(d) ? 0 : 1)
                .ToArray();

            var ums = found.FirstOrDefault(FelBootstrapService.LooksLikeConsoleDisk);
            ConsoleValue.Text = ums is not null
                ? "Console expondo o cartão (UMS)"
                : fel.Present
                    ? fel.DriverReady ? "Console em modo FEL · pronto" : "Console em FEL sem driver USB"
                    : "Nenhum console conectado";
            ConsoleDot.Foreground = ums is not null || (fel.Present && fel.DriverReady)
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84))
                : new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

            var previous = _target?.Number;
            _candidates.Clear();
            foreach (var disk in found)
                _candidates.Add(disk);
            _target = found.FirstOrDefault(d => d.Number == previous) ?? found.FirstOrDefault();

            CardValue.Text = _target is null ? "Nenhum cartão encontrado" : _target.Model;
            CardDetail.Text = _target is null ? "" : $"{_target.SizeGiB:0.00} GiB · {_target.Serial} · disco {_target.Number}";
            CardDot.Foreground = _target is not null
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84))
                : new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));

            BootstrapButton.Visibility = fel.Present && fel.DriverReady && ums is null && _target is null
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdatePrimary();
            UpdateTargets();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            ConsoleValue.Text = "Falha ao consultar o Windows";
            HintText.Text = ex.Message;
        }
    }

    private void UpdatePrimary()
    {
        if (_step != 1)
            return;
        PrimaryButton.IsEnabled = _packageVerified && _target is not null && !_bootstrapRunning;
        PrimaryButton.Content = _target is not null ? "Continuar" : "Aguardando o cartão";
        if (_packageVerified && _target is not null)
            FooterRight.Text = "Cartão pronto para receber o sistema";
        else if (!_packageVerified)
            FooterRight.Text = "Pacote ausente ou inválido";
        else
            FooterRight.Text = "Aguardando cartão";
    }

    private async void Bootstrap_Click(object sender, RoutedEventArgs e)
    {
        if (_bootstrapRunning)
            return;
        _bootstrapRunning = true;
        BootstrapButton.IsEnabled = false;
        HintText.Text = "Enviando o carregador para a memória do console…";
        try
        {
            var (ok, output, disk) = await _fel.RunAsync(
                FelBootstrapService.CurrentDiskNumbers(),
                message => Dispatcher.Invoke(() => HintText.Text = message),
                CancellationToken.None);
            HintText.Text = ok
                ? $"Carregador enviado. O cartão apareceu como disco {disk?.Number}."
                : "Falhou: " + LastLines(output, 2);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            HintText.Text = "Falha no bootstrap: " + ex.Message;
        }
        finally
        {
            _bootstrapRunning = false;
            BootstrapButton.IsEnabled = true;
            await PollAsync();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await PollAsync();

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null || !_packageVerified)
            return;
        ShowTargets();
        SetStep(2);
    }

    // ------------------------------------------------------------------ passo 2
    private void UpdateTargets()
    {
        if (_target is null)
            return;
        TargetTitle.Text = _target.Model;
        var connection = FelBootstrapService.LooksLikeConsoleDisk(_target) ? "pelo console (USB)" : "pelo leitor de cartões";
        TargetMeta.Text = $"{_target.SizeGiB:0.00} GiB · série {_target.Serial} · disco {_target.Number} · {connection}";
    }

    private void ShowTargets()
    {
        TargetList.SelectedItem = _target;
        UpdateTargets();
    }

    private void TargetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TargetList.SelectedItem is CardMedia picked)
            _target = picked;
        UpdateTargets();
    }

    private void ConfirmCheck_Changed(object sender, RoutedEventArgs e) => UpdateWriteButton();

    private void UpdateWriteButton() => WriteButton.IsEnabled = ConfirmCheck.IsChecked == true && _target is not null;

    private void Back_Click(object sender, RoutedEventArgs e) => SetStep(1);

    // ------------------------------------------------------------------ escrita
    private async void Write_Click(object sender, RoutedEventArgs e)
    {
        if (_target is not { } card || _package is not { } package || !_packageVerified || _writeRunning)
            return;

        Directory.CreateDirectory(_workDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var plan = new InstallPlanDocument
        {
            PackageId = "obsidian-os-1-0-lab-r3",
            SystemVersion = package.Version,
            Channel = "lab",
            ImagePath = package.ImagePath,
            ImageSha256 = package.Sha256,
            ImageBytes = package.Bytes,
            DiskNumber = card.Number,
            DiskModel = card.Model,
            DiskSerial = card.Serial,
            DiskBytes = card.SizeBytes,
            DataStartSector = 2097152,
            WorkDirectory = _workDirectory,
            LogPath = Path.Combine(_workDirectory, $"instalacao-{stamp}.log"),
            CleanBeforeWrite = true,
            PrepareDataPartition = true,
            FullReadback = !FelBootstrapService.LooksLikeConsoleDisk(card),
            TargetIsConsoleUms = FelBootstrapService.LooksLikeConsoleDisk(card)
        };

        _writeRunning = true;
        _writeCts = new CancellationTokenSource();
        SetStep(3);
        ResetStages();
        WriteHeadline.Text = "Gravando.";
        WriteLead.Text = "Não desconecte o cabo. Acompanhe cada etapa abaixo.";
        DonePanel.Visibility = Visibility.Collapsed;
        WriteBar.Value = 0;
        WriteStatus.Text = "Preparando a gravação…";
        WriteLog.Text = "";
        FooterRight.Text = "Gravando…";

        var planPath = InstallExecutor.WritePlan(plan, Path.Combine(_workDirectory, $"plano-{stamp}.json"));
        try
        {
            var result = InstallExecutor.IsElevated()
                ? await RunInProcessAsync(plan)
                : await RunElevatedAsync(planPath, stamp);
            ShowResult(result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WriteStatus.Text = "Falha: " + ex.Message;
            FooterRight.Text = "Falha na gravação";
        }
        finally
        {
            _writeRunning = false;
            _writeCts?.Dispose();
            _writeCts = null;
        }
    }

    private async Task<InstallResultDocument> RunInProcessAsync(InstallPlanDocument plan)
    {
        var progress = new Progress<WriteProgress>(p =>
        {
            WriteBar.Value = p.Percent;
            WriteStatus.Text = $"{p.Percent:0.0}% · {p.DoneBytes / 1048576:0} de {p.TotalBytes / 1048576:0} MiB · {p.BytesPerSecond / 1048576:0.0} MiB/s";
        });
        return await InstallExecutor.RunAsync(plan, AppendLog, progress, _writeCts!.Token);
    }

    private async Task<InstallResultDocument> RunElevatedAsync(string planPath, string stamp)
    {
        var resultPath = Path.Combine(_workDirectory, $"resultado-{stamp}.json");
        WriteStatus.Text = "Aguardando a autorização do Windows…";
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Executável não localizado.");
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--apply-plan \"{planPath}\" --result \"{resultPath}\"",
            UseShellExecute = true,
            Verb = "runas"
        };

        Process process;
        try
        {
            process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o processo elevado.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new InstallResultDocument
            {
                Success = false,
                Stage = "elevação",
                Message = "A autorização de administrador foi cancelada. Nada foi gravado."
            };
        }

        using (process)
        {
            var plan = InstallExecutor.ReadPlan(planPath);
            var shown = 0;
            while (!process.HasExited)
            {
                if (File.Exists(plan.LogPath))
                {
                    var lines = await ReadSharedLinesAsync(plan.LogPath);
                    if (lines.Count > shown)
                    {
                        foreach (var line in lines.Skip(shown))
                            AppendLog(line);
                        shown = lines.Count;
                    }
                }
                await Task.Delay(700, CancellationToken.None);
            }
            await process.WaitForExitAsync(CancellationToken.None);
        }

        if (!File.Exists(resultPath))
            return new InstallResultDocument
            {
                Success = false,
                Stage = "resultado",
                Message = "O processo elevado terminou sem relatório. Verifique o cartão antes de reutilizá-lo."
            };
        return InstallExecutor.ReadResult(resultPath);
    }

    private static async Task<List<string>> ReadSharedLinesAsync(string path)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }

    private void AppendLog(string line)
    {
        Dispatcher.Invoke(() =>
        {
            WriteLog.Text += line + "\n";
            WriteLog.ScrollToEnd();
            WriteStatus.Text = line;
            ApplyStage(line);
        });
    }

    private void ApplyStage(string line)
    {
        void Mark(TextBlock dot, TextBlock text, string state)
        {
            dot.Text = state switch { "done" => "✓", "active" => "●", _ => "○" };
            dot.Foreground = state switch
            {
                "done" => new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84)),
                "active" => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x1A)),
                _ => new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8))
            };
            text.Foreground = state == "pending"
                ? new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }

        if (line.Contains("Conferindo SHA-256")) Mark(S1Dot, S1Text, "active");
        else if (line.Contains("Hash da imagem confere")) Mark(S1Dot, S1Text, "done");
        else if (line.Contains("Apagando a tabela")) Mark(S2Dot, S2Text, "active");
        else if (line.Contains("Cartão limpo")) Mark(S2Dot, S2Text, "done");
        else if (line.Contains("Testando o enlace")) Mark(S3Dot, S3Text, "active");
        else if (line.Contains("Enlace de gravação respondeu")) Mark(S3Dot, S3Text, "done");
        else if (line.Contains("Gravando a imagem")) Mark(S4Dot, S4Text, "active");
        else if (line.Contains("Imagem gravada")) Mark(S4Dot, S4Text, "done");
        else if (line.Contains("Relendo")) Mark(S5Dot, S5Text, "active");
        else if (line.Contains("Releitura confere")) { Mark(S4Dot, S4Text, "done"); Mark(S5Dot, S5Text, "done"); }
        else if (line.Contains("Formatando a área de dados") || line.Contains("Declarando a partição")) Mark(S6Dot, S6Text, "active");
        else if (line.Contains("Área de dados criada") || line.Contains("pasta de dados")) Mark(S6Dot, S6Text, "done");
    }

    private void ResetStages()
    {
        foreach (var (dot, text) in new[] { (S1Dot, S1Text), (S2Dot, S2Text), (S3Dot, S3Text), (S4Dot, S4Text), (S5Dot, S5Text), (S6Dot, S6Text) })
        {
            dot.Text = "○";
            dot.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
            text.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
        }
    }

    private void ShowResult(InstallResultDocument result)
    {
        if (result.Success)
        {
            WriteHeadline.Text = "Pronto.";
            WriteLead.Text = "O sistema foi gravado e conferido. O próximo passo é do console.";
            WriteBar.Value = 100;
            foreach (var (dot, text) in new[] { (S1Dot, S1Text), (S2Dot, S2Text), (S3Dot, S3Text), (S4Dot, S4Text), (S5Dot, S5Text), (S6Dot, S6Text) })
            {
                dot.Text = "✓";
                dot.Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84));
                text.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            }
            DonePanel.Visibility = Visibility.Visible;
            WriteStatus.Text = $"{result.Message} ({result.Seconds:0} s)";
            FooterRight.Text = "Cartão pronto";
        }
        else
        {
            WriteHeadline.Text = "Não concluído.";
            WriteLead.Text = "Nada foi gravado ou a gravação foi interrompida. Reveja a mensagem abaixo.";
            WriteStatus.Text = $"{result.Stage}: {result.Message}";
            FooterRight.Text = "Gravação não concluída";
        }
    }

    private void Again_Click(object sender, RoutedEventArgs e)
    {
        ConfirmCheck.IsChecked = false;
        _target = null;
        SetStep(1);
        _ = PollAsync();
    }

    // ------------------------------------------------------------------ passos
    private void SetStep(int step)
    {
        _step = step;
        PageConnect.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageCard.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageWrite.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageRestore.Visibility = Visibility.Collapsed;

        var accent = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x1A));
        var muted = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8));
        RailNum1.Foreground = step == 1 ? accent : muted;
        RailNum2.Foreground = step == 2 ? accent : muted;
        RailNum3.Foreground = step >= 3 ? accent : muted;
        UpdateWriteButton();
    }

    private void Rail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var step))
            return;
        if (step == 1) { SetStep(1); _ = PollAsync(); }
        else if (step == 2 && _target is not null) { ShowTargets(); SetStep(2); }
        else if (step == 3 && _writeRunning) SetStep(3);
    }

    // ------------------------------------------------------------------ conteúdo
    private void GoRestore_Click(object sender, RoutedEventArgs e)
    {
        PageConnect.Visibility = Visibility.Collapsed;
        PageCard.Visibility = Visibility.Collapsed;
        PageWrite.Visibility = Visibility.Collapsed;
        PageRestore.Visibility = Visibility.Visible;
        LoadRestoreVolumes();
    }

    private void LoadRestoreVolumes()
    {
        RestoreVolume.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is DriveType.Network or DriveType.NoRootDirectory)
                    continue;
                var text = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? $"{drive.RootDirectory.FullName} · {drive.AvailableFreeSpace / 1073741824d:0.0} GiB livres"
                    : $"{drive.VolumeLabel} ({drive.RootDirectory.FullName.TrimEnd('\\')}) · {drive.AvailableFreeSpace / 1073741824d:0.0} GiB livres";
                RestoreVolume.Items.Add(new ComboBoxItem { Content = text, Tag = drive.RootDirectory.FullName });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        if (RestoreVolume.Items.Count > 0)
            RestoreVolume.SelectedIndex = 0;
    }

    private async void RestoreFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Pasta com livros, jogos ou vídeos" };
        if (dialog.ShowDialog(this) != true) return;
        await ScanContentAsync(new FolderContentSource(dialog.FolderName));
    }

    private async void RestoreImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Imagem de conteúdo", Filter = "Imagem (*.img)|*.img|Todos (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var source = await Task.Run(() => Fat32ImageSource.Open(dialog.FileName));
            await ScanContentAsync(source);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            RestoreSource.Text = "Não foi possível abrir a imagem: " + ex.Message;
        }
    }

    private async Task ScanContentAsync(IContentSource source)
    {
        (_contentSource as IDisposable)?.Dispose();
        _contentSource = source;
        RestoreSource.Text = source.DisplayName;
        RestoreItems.Items.Clear();
        try
        {
            var items = await Task.Run(source.ScanContent);
            foreach (var item in items)
                RestoreItems.Items.Add(new TextBlock
                {
                    Text = $"• {item.Label}: {item.FileCount} arquivo(s) · {item.SizeText}",
                    Foreground = (Brush)FindResource("Ink"),
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            RestoreStatus.Text = items.Count == 0
                ? "Nenhuma pasta reconhecida (roms, leitura, videos, saves)."
                : "Escolha o destino e clique em Restaurar.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            RestoreStatus.Text = "Falha ao ler a origem: " + ex.Message;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_contentSource is null || RestoreVolume.SelectedItem is not ComboBoxItem { Tag: string destination })
        {
            RestoreStatus.Text = "Escolha a origem e o destino.";
            return;
        }
        try
        {
            var kinds = ContentKinds.All.ToArray();
            var restorer = new ContentRestorer(_workDirectory);
            var outcome = await restorer.RestoreAsync(_contentSource,
                new RestoreOptions(destination, kinds, ConflictPolicy.Skip), null, CancellationToken.None);
            RestoreStatus.Text = $"Copiados {outcome.Copied} · substituídos {outcome.Replaced} · mantidos {outcome.Skipped} · falhas {outcome.Failed}. Relatório: {outcome.ReportPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            RestoreStatus.Text = "Falha na restauração: " + ex.Message;
        }
    }

    private static string LastLines(string text, int count)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" | ", lines.TakeLast(count));
    }
}
