using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Narod.SteamGameFinder;
using Octokit;
using SharpCompress.Archives;
using SharpCompress.Common;

class Program
{
    // ID do Persona 3 Portable na Steam
    const string P3P_APP_ID = "1809700";
    const string GITHUB_OWNER = "Hinrong";
    const string GITHUB_REPO = "P3P-Traduzido";

    static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("=================================================");
        Console.WriteLine("    INSTALADOR DE TRADUÇÃO - PERSONA 3 PORTABLE  ");
        Console.WriteLine("=================================================");
        Console.WriteLine("AVISO: Esta tradução funciona SOMENTE na versão da STEAM.");
        Console.WriteLine("Certifique-se de que o jogo está instalado e atualizado.");
        Console.WriteLine("Pressione qualquer tecla para continuar...");
        Console.ResetColor();
        Console.ReadKey();

        try
        {
            // 1. Achar o diretório do jogo
            Console.WriteLine("\n[1/5] Localizando instalação do Persona 3 Portable...");
            var gameInfo = new SteamGameLocator();
            var gameInfoResult = gameInfo.getGameInfoByID(P3P_APP_ID);
            string gamePath = gameInfoResult.steamGameLocation;

            if (gameInfo == null || string.IsNullOrEmpty(gamePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Erro: Jogo não encontrado! Verifique se está instalado na Steam.");
                return;
            }

            string gameDir = gamePath;
            Console.WriteLine($" -> Jogo encontrado em: {gameDir}");

            // 2. Achar o link de download do GitHub
            Console.WriteLine("[2/5] Buscando última versão da tradução...");
            string downloadUrl = await GetLatestReleaseUrl();
            Console.WriteLine($" -> Link encontrado: {downloadUrl}");

            // 3. Baixar o arquivo
            string zipPath = Path.Combine(Path.GetTempPath(), "p3p_traducao.zip");
            Console.WriteLine("[3/5] Baixando arquivos (isso pode demorar um pouco)...");
            await DownloadFileAsync(downloadUrl, zipPath);

            // 4. Extrair com SharpCompress
            string extractPath = Path.Combine(Directory.GetCurrentDirectory(), "P3PBR");
            Console.WriteLine($"[4/5] Extraindo para pasta temporária '{extractPath}'...");

            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);

            ExtractZip(zipPath, extractPath);

            // 5. Instalar (Copiar arquivos específicos)
            Console.WriteLine("[5/5] Instalando arquivos na pasta do jogo...");
            InstallFiles(extractPath, gameDir);

            // Limpeza
            File.Delete(zipPath);
            // Opcional: Deletar a pasta P3PBR após instalar
            // Directory.Delete(extractPath, true); 

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nSUCESSO! A tradução foi instalada.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nOcorreu um erro crítico: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("\nPressione qualquer tecla para sair.");
        Console.ReadKey();
    }

    static async Task<string> GetLatestReleaseUrl()
    {
        var client = new GitHubClient(new ProductHeaderValue("P3P-Installer"));
        var releases = await client.Repository.Release.GetAll(GITHUB_OWNER, GITHUB_REPO);

        var latest = releases.FirstOrDefault();
        if (latest == null) throw new Exception("Nenhuma release encontrada no repositório.");

        // Pega o primeiro asset (geralmente o zip)
        var asset = latest.Assets.FirstOrDefault(x => x.Name.EndsWith(".zip") || x.Name.EndsWith(".7z") || x.Name.EndsWith(".rar"));

        if (asset == null) throw new Exception("Nenhum arquivo compactado encontrado na release.");

        return asset.BrowserDownloadUrl;
    }

    static async Task DownloadFileAsync(string url, string outputPath)
    {
        using var httpClient = new HttpClient();
        var data = await httpClient.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(outputPath, data);
    }

    static void ExtractZip(string zipPath, string outFolder)
    {
        using (var archive = ArchiveFactory.Open(zipPath))
        {
            foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(outFolder, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
        }
    }

    static void InstallFiles(string sourceFolder, string gameFolder)
    {
        // Estrutura esperada dentro de P3PBR: "update" (pasta) e "dinput8.dll" (arquivo)

        // 1. Copiar dinput8.dll
        string dllSource = Path.Combine(sourceFolder, "dinput8.dll");
        string dllDest = Path.Combine(gameFolder, "dinput8.dll");

        if (File.Exists(dllSource))
        {
            File.Copy(dllSource, dllDest, true);
            Console.WriteLine(" -> dinput8.dll copiado.");
        }
        else
        {
            Console.WriteLine(" -> AVISO: dinput8.dll não foi encontrado na extração.");
        }

        // 2. Copiar pasta update (Recursivo)
        string updateSource = Path.Combine(sourceFolder, "update");
        string updateDest = Path.Combine(gameFolder, "update");

        if (Directory.Exists(updateSource))
        {
            CopyDirectory(updateSource, updateDest);
            Console.WriteLine(" -> Pasta 'update' copiada.");
        }
        else
        {
            Console.WriteLine(" -> AVISO: Pasta 'update' não foi encontrada na extração.");
        }
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Diretório não encontrado: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}