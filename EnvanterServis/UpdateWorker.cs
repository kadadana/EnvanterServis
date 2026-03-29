using EnvanterServis.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;

public class UpdateWorker
{
    Logger logger = new Logger();
    private static readonly string DownloadUrl = "http://192.168.1.210:9000/deployments/EnvanterServis/latest/EnvanterServis.zip";
    private static readonly string VersionUrl = "http://192.168.1.210:9000/deployments/EnvanterServis/version.txt";
    private static readonly string CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
    private static readonly string ServiceName = "EnvanterServis";

    public async Task CheckUpdateSilently()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "EnvanterServis-Updater");

                string latestVersion = (await client.GetStringAsync(VersionUrl)).Trim();

                if (latestVersion != CurrentVersion)
                {
                    logger.LogWithMessage($"Yeni versiyon bulundu: {latestVersion}");

                    string servicePath = AppDomain.CurrentDomain.BaseDirectory;
                    string zipPath = Path.Combine(servicePath, "update.zip");
                    string extractPath = Path.Combine(servicePath, "temp_update");

                    byte[] zipBytes = await client.GetByteArrayAsync(DownloadUrl);
                    File.WriteAllBytes(zipPath, zipBytes);

                    if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                    ZipFile.ExtractToDirectory(zipPath, extractPath);

                    ApplyZipUpdate(servicePath, zipPath, extractPath);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWithMessage("Güncelleme hatası: " + ex.Message);
        }
    }
    private void ApplyZipUpdate(string servicePath, string zipPath, string extractPath)
    {
        string currentExe = Path.Combine(servicePath, "EnvanterServis.exe");

        string cmdCommands = $"/c net stop {ServiceName} & " +
                             $"timeout /t 5 & " +
                             $"xcopy /y /s \"{extractPath}\\*\" \"{servicePath}\" & " +
                             $"del /f /q \"{zipPath}\" & " +
                             $"rd /s /q \"{extractPath}\" & " +
                             $"net start {ServiceName} & " +
                             $"sc config {ServiceName} start= delayed-auto";

        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmdCommands)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            Verb = "runas"
        };

        logger.LogWithMessage("Servis durduruluyor ve dosyalar güncelleniyor...");
        Process.Start(psi);

        Environment.Exit(0);
    }
}