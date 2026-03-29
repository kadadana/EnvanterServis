using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using System.Globalization;
using EnvanterServis.Helpers;

namespace EnvanterServis
{
    public partial class Service1 : ServiceBase
    {
        private UpdateWorker _updateWorker = new UpdateWorker();
        Logger logger = new Logger();
        string apiKey = Environment.GetEnvironmentVariable("EYP_SERVICE_KEY");
        string seriNo;
        string computerName;
        string ramGB;
        long totalDiskGB = 0;
        string macAddress;
        string userName;
        string islemci;
        string model;
        string driveInfo;
        string osName;
        string osVer;
        string lastIpAddress = "";
        readonly HttpClient _httpClient = new HttpClient();

        static string programYolu = AppDomain.CurrentDomain.BaseDirectory.ToString();
        string _serverUrl = "http://192.168.1.210:5105/api/inventory";
        Timer inventoryTimer = new Timer();
        Timer logTimer = new Timer();
        Timer updateTimer = new Timer();
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            logger.LogWithMessage("Servis çalışmaya başladı." + DateTime.Now);

            inventoryTimer.Interval = 1000 * 60 * 5;
            inventoryTimer.Elapsed += InventoryTimerElapsed;
            inventoryTimer.Start();

            updateTimer.Interval = 1000 * 60 * 5;
            updateTimer.Elapsed += UpdateTimerElapsed;
            updateTimer.Start();

            logTimer.Interval = 1000 * 60 * 60;
            logTimer.Elapsed += LogTimerElapsed;
            logTimer.Start();

            _ = Task.Run(() => InventoryTimerElapsed(null, null));
            _ = Task.Run(() => UpdateTimerElapsed(null, null));
        }

        private static void LogTimerElapsed(object source, ElapsedEventArgs e)
        {
            File.WriteAllText(programYolu + "\\Logs\\Log.txt", string.Empty);
        }

        protected override void OnStop()
        {
            logger.LogWithMessage("Servis durdu." + DateTime.Now + "\n\n");
            inventoryTimer.Stop();
        }
        private async void InventoryTimerElapsed(object sender, ElapsedEventArgs e)
        {
            logger.LogWithMessage("Servis Çalışıyor. " + DateTime.Now);
            try
            {
                string veri = EnvanterBilgileriniAl();
                await EnvanterBilgileriniGonder(veri);
                if (DateTime.Now.Hour == 15 && DateTime.Now.Minute == 0)
                {
                    await EnvanterBilgileriniGonder(veri);
                }
            }
            catch (Exception ex)
            {
                logger.LogWithMessage($"Hata: " + ex.Message);

            }
        }
        private async void UpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            _ = Task.Run(async () => await _updateWorker.CheckUpdateSilently());
        }
        private string EnvanterBilgileriniAl()
        {
            ulong ramCapacity;

            ManagementObjectSearcher biosSearcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (ManagementObject obj in biosSearcher.Get().Cast<ManagementObject>())
            {
                seriNo = obj["SerialNumber"].ToString();


            }

            ManagementObjectSearcher compSearcher = new ManagementObjectSearcher("SELECT Name, Model, TotalPhysicalMemory, UserName FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in compSearcher.Get().Cast<ManagementObject>())
            {
                computerName = obj["Name"].ToString();
                userName = obj["UserName"].ToString();
                userName = userName.Split('\\')[1];
                model = obj["Model"].ToString();
                ramCapacity = (ulong)obj["TotalPhysicalMemory"];
                ramGB = Math.Ceiling((decimal)ramCapacity / 1073741824).ToString("F2", CultureInfo.InvariantCulture);
            }

            ManagementObjectSearcher osSearcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in osSearcher.Get().Cast<ManagementObject>())
            {
                osName = obj["Caption"].ToString();
                osVer = obj["Version"].ToString();

            }

            StringBuilder sb = new StringBuilder();
            var readyDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            totalDiskGB = 0;
            for (int i = 0; i < readyDrives.Count; i++)
            {
                var drive = readyDrives[i];

                totalDiskGB += drive.TotalSize / (1024 * 1024 * 1024);
                sb.AppendLine("    {");
                sb.AppendLine($"    \"Name\": \"{drive.Name}\\\",");
                sb.AppendLine($"    \"TotalSizeGB\": {(drive.TotalSize / (1024 * 1024 * 1024)).ToString("F2", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"TotalFreeSpaceGB\": {(drive.TotalFreeSpace / (1024 * 1024 * 1024)).ToString("F2", CultureInfo.InvariantCulture)}");

                if (i == readyDrives.Count - 1)
                    sb.AppendLine("    }");
                else
                    sb.AppendLine("    },");

            }

            driveInfo = sb.ToString();



            ManagementObjectSearcher macSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            foreach (ManagementObject obj in macSearcher.Get().Cast<ManagementObject>())
            {
                macAddress = obj["MacAddress"].ToString();

            }


            ManagementObjectSearcher procSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject obj in procSearcher.Get().Cast<ManagementObject>())
            {
                islemci = obj["Name"].ToString().Trim();

            }

            lastIpAddress = GetLocalIPv4();


            return "{\n" +
                $"\"SeriNo\": \"{seriNo}\",\n" +
                $"\"CompModel\": \"{model}\",\n" +
                $"\"CompName\": \"{computerName}\",\n" +
                $"\"RAM\": {ramGB},\n" +
                $"\"DiskGB\": {totalDiskGB.ToString("F2", CultureInfo.InvariantCulture)},\n" +
                $"\"MAC\": \"{macAddress}\",\n" +
                $"\"ProcModel\": \"{islemci}\",\n" +
                $"\"Username\": \"{userName}\",\n" +
                $"\"OsName\": \"{osName}\",\n" +
                $"\"OsVer\": \"{osVer}\",\n" +
                $"\"Drives\": [\n" +
                $"{driveInfo}" +
                $"],\n" +
                $"\"LastIpAddress\": \"{lastIpAddress}\",\n" +
                $"\"DateChanged\": \"{DateTime.Now.ToString("o")}\"\n" +
                "}";




        }
        private async Task EnvanterBilgileriniGonder(string veri)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _serverUrl);
            request.Headers.Add("EYP_API_KEY", apiKey);
            request.Content = new StringContent(veri, Encoding.UTF8, "application/json");

            logger.LogWithMessage($"Sunucuya({_serverUrl}) gönderilmeye calisiliyor:\n");

            try
            {
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogWithMessage($"-------------------------------------\n{veri}" + "\nSunucuya basariyla gonderildi." + "\n-------------------------------------");
                }
                else
                {
                    logger.LogWithMessage($"Sunucuya gönderilemedi. Hata kodu: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWithMessage($"HATA: {ex.Message}");
                logger.LogWithMessage($"Inner Exception: {ex.InnerException?.Message}");
                logger.LogWithMessage($"Stack Trace: {ex.StackTrace}");
            }
        }
        private string GetLocalIPv4()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.Description.ToLower().Contains("virtual") ||
                    ni.Name.ToLower().Contains("vbox") ||
                    ni.Description.ToLower().Contains("hyper-v") ||
                    ni.Name.ToLower().Contains("docker"))
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (var ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.Address.ToString();
                    }
                }
            }
            return "null";
        }

    }
}
