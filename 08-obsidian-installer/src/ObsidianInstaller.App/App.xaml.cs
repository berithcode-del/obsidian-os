using System.IO;
using System.Windows;

namespace ObsidianInstaller.App;

public partial class App : Application
{
    private SplashWindow? _splash;

    protected override void OnStartup(StartupEventArgs e)
    {
        var mediaIndex = Array.IndexOf(e.Args, "--list-media");
        if (mediaIndex >= 0 && mediaIndex + 1 < e.Args.Length)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown(MediaInventoryExport.Run(e.Args[mediaIndex + 1]));
            return;
        }

        var planIndex = Array.IndexOf(e.Args, "--apply-plan");
        if (planIndex >= 0 && planIndex + 1 < e.Args.Length)
        {
            // Modo elevado: sem janela, só executa o plano e grava o resultado.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var planPath = e.Args[planIndex + 1];
            var resultIndex = Array.IndexOf(e.Args, "--result");
            var resultPath = resultIndex >= 0 && resultIndex + 1 < e.Args.Length
                ? e.Args[resultIndex + 1]
                : Path.ChangeExtension(planPath, ".result.json");
            var exitCode = RunPlan(planPath, resultPath);
            Shutdown(exitCode);
            return;
        }

        base.OnStartup(e);
        try
        {
            ShowInstaller();
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            MessageBox.Show("O instalador não conseguiu abrir: " + ex.Message, "Obsidian OS",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
        }
    }

    private void ShowInstaller()
    {
        // Splash com a arte do console: fecha quando a janela principal aparece.
        _splash = new SplashWindow();
        _splash.Show();
        _splash.SetStatus("Verificando o pacote do sistema…");

        // Tempo mínimo na tela: sem isso o ContentRendered da janela principal fecha a
        // splash antes de ela aparecer (o usuário via só a janela abrindo).
        var holdMs = 1500;
        if (int.TryParse(Environment.GetEnvironmentVariable("OBSIDIAN_SPLASH_HOLD_MS"), out var hold) && hold > 0)
            holdMs = hold;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(holdMs) };
        timer.Tick += (_, _) => { timer.Stop(); _splash?.Close(); _splash = null; };
        timer.Start();

        var window = new MainWindow();
        MainWindow = window;

        window.Show();
    }

    private static void LogStartupFailure(Exception ex)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "work");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "startup-error.log"), ex.ToString());
        }
        catch (IOException)
        {
        }
    }

    private static int RunPlan(string planPath, string resultPath)
    {
        try
        {
            var plan = InstallExecutor.ReadPlan(planPath);
            var result = InstallExecutor.RunAsync(plan, _ => { }, null, CancellationToken.None)
                .GetAwaiter().GetResult();
            InstallExecutor.WriteResult(result, resultPath);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            var failure = new InstallResultDocument
            {
                Success = false,
                Stage = "plano",
                Message = "O plano elevado não pôde ser executado: " + ex.Message
            };
            try
            {
                InstallExecutor.WriteResult(failure, resultPath);
            }
            catch (IOException)
            {
            }
            return 2;
        }
    }
}
