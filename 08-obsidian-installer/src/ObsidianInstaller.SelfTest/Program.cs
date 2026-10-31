using System.Security.Cryptography;
using System.Text.Json;
using ObsidianInstaller.Core;

namespace ObsidianInstaller.SelfTest;

/// <summary>
/// Exercita o núcleo de gravação e restauração sem tocar em cartão físico:
/// usa arquivos como destino (cartão simulado) e a imagem de conteúdo real.
/// </summary>
internal static class Program
{
    private static int _failures;
    private static int _checks;

    private static void Check(string description, bool condition, string? detail = null)
    {
        _checks++;
        if (condition)
        {
            Console.WriteLine($"  ok   {description}");
            return;
        }
        _failures++;
        Console.WriteLine($"  FAIL {description}{(detail is null ? "" : $" — {detail}")}");
    }

    private static int Main(string[] args)
    {
        var work = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "selftest");
        Directory.CreateDirectory(work);
        Console.WriteLine($"Self-test do Obsidian Installer · diretório {work}");

        var root = FindProjectRoot();
        Console.WriteLine($"Raiz do projeto: {root}");

        var image = Path.Combine(root, "artifacts", "releases", "obsidian-os-1.0-r1", "obsidian-os-1.0-lab-r1.img");
        var manifest = Path.Combine(root, "artifacts", "releases", "obsidian-os-1.0-r1", "obsidian-os-1.0-lab-r1.manifest.json");
        var contentImage = Path.Combine(root, "artifacts", "releases", "obsidian-conteudo-livros-1.0-lab.img");
        var contentManifest = Path.Combine(root, "artifacts", "releases", "obsidian-conteudo-livros-1.0-lab.json");
        var booksFolder = Path.Combine(root, "..", "Livros", "Colecoes");

        TestMbr(image);
        TestWriteAndLayout(work, image, manifest);
        TestContentImage(contentImage, contentManifest, work);
        TestRestoreFromFolder(booksFolder, work);

        Console.WriteLine();
        Console.WriteLine($"{_checks - _failures}/{_checks} verificações aprovadas.");
        return _failures == 0 ? 0 : 1;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ObsidianInstaller.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private static void TestMbr(string imagePath)
    {
        Console.WriteLine("\n[1] Tabela de partições da imagem");
        if (!File.Exists(imagePath))
        {
            Check("imagem de sistema presente", false, imagePath);
            return;
        }
        var sector = new byte[512];
        using (var stream = File.OpenRead(imagePath))
            stream.ReadExactly(sector, 0, 512);
        var parts = MbrTable.Parse(sector);
        Check("duas partições na imagem", parts.Count == 2, $"encontradas {parts.Count}");
        var root = parts[0];
        var home = parts[1];
        Check("p1 é ext4 em 262144", root.Type == 0x83 && root.StartSector == 262144, $"{root.Type:X2}@{root.StartSector}");
        Check("p2 é FAT32 em 1048576", home.Type == 0x0C && home.StartSector == 1048576, $"{home.Type:X2}@{home.StartSector}");
        Check("p2 termina em 2097152", home.EndSector == 2097152, home.EndSector.ToString());

        const long cardBytes = 15485370368; // cartão de 14,42 GiB observado no console
        var cardSectors = (uint)(cardBytes / 512);
        var dataStart = 2097152u;
        var dataSectors = cardSectors - dataStart;
        var patched = MbrTable.WithDataPartition(sector, dataStart, dataSectors);
        var withData = MbrTable.Parse(patched);
        Check("terceira partição declarada", withData.Count == 3, $"encontradas {withData.Count}");
        var data = withData[2];
        Check("p3 começa depois da home", data.StartSector == dataStart, data.StartSector.ToString());
        Check("p3 cobre o resto do cartão", data.EndSector == cardSectors, $"{data.EndSector} vs {cardSectors}");
        Check("p3 é FAT32", data.Type == 0x0C, $"{data.Type:X2}");
        Check("assinatura 0x55AA presente", patched[510] == 0x55 && patched[511] == 0xAA);
        Console.WriteLine($"     p3: {data.StartSector}..{data.EndSector} setores ({(data.EndSector - data.StartSector) * 512L / 1073741824d:0.00} GiB)");
    }

    private static void TestWriteAndLayout(string work, string imagePath, string manifestPath)
    {
        Console.WriteLine("\n[2] Gravação em cartão simulado (arquivo)");
        if (!File.Exists(imagePath) || !File.Exists(manifestPath))
        {
            Check("imagem e manifesto presentes", false);
            return;
        }
        using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var payload = manifestDoc.RootElement.GetProperty("payload");
        var expectedHash = payload.GetProperty("sha256").GetString();
        var imageBytes = payload.GetProperty("image_bytes").GetInt64();

        var cardPath = Path.Combine(work, "cartao-simulado.img");
        const long cardBytes = 15485370368;
        using (var card = new FileStream(cardPath, FileMode.Create, FileAccess.ReadWrite))
        {
            card.SetLength(cardBytes);   // arquivo esparso: não ocupa 14 GiB reais
        }

        using (var card = new FileStream(cardPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            using var source = File.OpenRead(imagePath);
            var result = ImageWriter.WriteAsync(source, card, imageBytes, null, CancellationToken.None).GetAwaiter().GetResult();
            Check("hash calculado durante a gravação confere", string.Equals(result.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase), result.Sha256);
            Check("bytes gravados iguais ao manifesto", result.Bytes == imageBytes, $"{result.Bytes}");

            var verify = ImageWriter.VerifyAsync(card, imageBytes, expectedHash!, null, CancellationToken.None).GetAwaiter().GetResult();
            Check("releitura do cartão simulado confere", verify.Matches, verify.ActualSha256);

            card.Position = 0;
            var sector = new byte[512];
            card.ReadExactly(sector, 0, 512);
            var totalSectors = (uint)(cardBytes / 512);
            var patched = MbrTable.WithDataPartition(sector, 2097152, totalSectors - 2097152);
            card.Position = 0;
            card.Write(patched, 0, patched.Length);
            card.Flush();

            card.Position = 0;
            card.ReadExactly(sector, 0, 512);
            var parts = MbrTable.Parse(sector);
            Check("cartão simulado ficou com 3 partições", parts.Count == 3, $"encontradas {parts.Count}");
            var fileLength = new FileInfo(cardPath).Length;
            var onDisk = new FileInfo(cardPath).Length > 0;
            Check("arquivo do cartão simulado mantém 14,42 GiB lógicos", onDisk && fileLength == cardBytes, fileLength.ToString());
        }

        var reportPath = Path.Combine(work, "selftest-gravacao.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            card = cardPath,
            card_bytes = cardBytes,
            image_bytes = imageBytes,
            image_sha256 = expectedHash,
            simulated = true
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"     cartão simulado: {cardPath}");
    }

    private static void TestContentImage(string contentImage, string contentManifest, string work)
    {
        Console.WriteLine("\n[3] Leitura da imagem de conteúdo (FAT32)");
        if (!File.Exists(contentImage) || !File.Exists(contentManifest))
        {
            Check("imagem e manifesto de conteúdo presentes", false);
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(contentManifest));
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToArray();

        using var source = Fat32ImageSource.Open(contentImage);
        var items = source.ScanContent();
        var leitura = items.FirstOrDefault(i => i.Kind == ContentKinds.Leitura);
        Check("pasta leitura reconhecida", leitura is not null);
        Check("86 livros na imagem", leitura?.FileCount == 86, leitura?.FileCount.ToString());
        Check("pastas vazias roms/videos/saves reconhecidas",
            items.Any(i => i.Kind == ContentKinds.Roms) && items.Any(i => i.Kind == ContentKinds.Videos) &&
            items.Any(i => i.Kind == ContentKinds.Saves));

        var files = source.EnumerateFiles(ContentKinds.Leitura);
        var sample = entries.First();
        var samplePath = sample.GetProperty("file").GetString()!;
        var sampleSha = sample.GetProperty("sha256").GetString()!;
        var match = files.FirstOrDefault(f => string.Equals(f.RelativePath, samplePath, StringComparison.OrdinalIgnoreCase));
        Check("arquivo de exemplo listado", match is not null, samplePath);
        if (match is not null)
        {
            using var stream = source.OpenRead(match);
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            Check("conteúdo lido do FAT32 confere com o manifesto", hash == sampleSha, hash);
        }

        // Restauração da imagem para uma pasta (o cartão real usa a mesma cópia).
        var destination = Path.Combine(work, "restauracao-da-imagem");
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        Directory.CreateDirectory(destination);
        var restorer = new ContentRestorer(work);
        var outcome = restorer.RestoreAsync(source,
            new RestoreOptions(destination, [ContentKinds.Leitura], ConflictPolicy.Skip), null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Check("86 livros copiados da imagem", outcome.Copied == 86, outcome.Copied.ToString());
        Check("nenhuma falha de cópia", outcome.Failed == 0, outcome.Failed.ToString());
        Check("relatório da restauração gravado", File.Exists(outcome.ReportPath));
        var copiedBytes = new DirectoryInfo(Path.Combine(destination, "leitura")).EnumerateFiles().Sum(f => f.Length);
        Check("tamanho copiado igual ao planejado", copiedBytes == leitura!.TotalBytes, $"{copiedBytes} vs {leitura.TotalBytes}");

        var second = restorer.RestoreAsync(source,
            new RestoreOptions(destination, [ContentKinds.Leitura], ConflictPolicy.KeepBoth), null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Check("política manter as duas cópias renomeia", second.Renamed == 86, second.Renamed.ToString());
    }

    private static void TestRestoreFromFolder(string booksFolder, string work)
    {
        Console.WriteLine("\n[4] Restauração a partir de pasta de backup");
        if (!Directory.Exists(booksFolder))
        {
            Check("pasta de livros acessível", false, booksFolder);
            return;
        }
        var source = new FolderContentSource(booksFolder);
        var items = source.ScanContent();
        var leitura = items.FirstOrDefault(i => i.Kind == ContentKinds.Leitura);
        Check("livros reconhecidos na pasta", leitura is not null && leitura.FileCount > 0, leitura?.FileCount.ToString());
        if (leitura is null)
            return;

        var destination = Path.Combine(work, "restauracao-da-pasta");
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        Directory.CreateDirectory(destination);
        var restorer = new ContentRestorer(work);
        var outcome = restorer.RestoreAsync(source,
            new RestoreOptions(destination, [ContentKinds.Leitura], ConflictPolicy.Skip), null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Check("livros copiados da pasta", outcome.Copied == leitura.FileCount, $"{outcome.Copied} vs {leitura.FileCount}");
        Check("sem falhas", outcome.Failed == 0, outcome.Failed.ToString());
    }
}
