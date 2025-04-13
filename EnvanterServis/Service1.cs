using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;

namespace EnvanterServis
{
    public partial class Service1 : ServiceBase
    {
        string seriNo;
        string computerName;
        string ramGB;
        long totalDiskGB = 0;
        string macAddress;
        string userName;
        string islemci;
        string model;
        string driveInfo;
        string lastIpAddress = "";
        readonly HttpClient _httpClient = new HttpClient();
        static string programYolu = AppDomain.CurrentDomain.BaseDirectory.ToString();
        string xmlPath = programYolu + "\\appconfig.xml";
        string _serverUrl;
        Timer timer = new Timer();
        Timer timer2 = new Timer();
        public Service1()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            Logger("Servis çalışmaya başladı." + DateTime.Now);

            if (File.Exists(xmlPath))
            {
                _serverUrl = ServerURLfromXML(xmlPath);
            }
            else
            {
                Logger("Sunucu yolu bulunamadi.");
            }
            string veri = EnvanterBilgileriniAl();
            await EnvanterBilgileriniGonder(veri);
            timer.Interval = 1000 * 60 * 5;
            timer.Elapsed += new ElapsedEventHandler(TimerElapsed);
            timer.Enabled = true;
            timer.Start();
            timer2.Interval = 1000 * 60 * 60;
            timer2.Elapsed += new ElapsedEventHandler(Timer2Elapsed);
            timer2.Enabled = true;
            timer2.Start();



        }

        private static void Timer2Elapsed(object source, ElapsedEventArgs e)
        {
            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\Log.txt");
        }

        protected override void OnStop()
        {
            Logger("Servis durdu." + DateTime.Now + "\n\n");
            timer.Stop();
        }
        private async void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger("Servis Çalışıyor." + DateTime.Now);
            string veri = EnvanterBilgileriniAl();
            await EnvanterBilgileriniGonder(veri);
            if (DateTime.Now.Hour == 15 && DateTime.Now.Minute == 0)
            {
                await EnvanterBilgileriniGonder(veri);
                Logger($"Log'a yazildi:\n {veri}");
            }
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
                ramGB = Math.Ceiling((decimal)(ramCapacity) / 1073741824).ToString("F2");

            }

            StringBuilder sb = new StringBuilder();
            var readyDrives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
            for (int i = 0; i < readyDrives.Count; i++)
            {
                var drive = readyDrives[i];

                totalDiskGB += drive.TotalSize / (1024 * 1024 * 1024);
                sb.AppendLine("    {");
                sb.AppendLine($"    \"Name\": \"{drive.Name}\\\",");
                sb.AppendLine($"    \"TotalSizeGB\": \"{(drive.TotalSize / (1024 * 1024 * 1024)).ToString("F2")}\",");
                sb.AppendLine($"    \"TotalFreeSpaceGB\": \"{(drive.TotalFreeSpace / (1024 * 1024 * 1024)).ToString("F2")}\"");

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

            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    lastIpAddress = ip.ToString();
                    break;
                }
            }

            return "{\n" +
                $"\"SeriNo\": \"{seriNo}\",\n" +
                $"\"CompModel\": \"{model}\",\n" +
                $"\"CompName\": \"{computerName}\",\n" +
                $"\"RAM\": \"{ramGB}\",\n" +
                $"\"DiskGB\": \"{totalDiskGB.ToString("F2")}\",\n" +
                $"\"MAC\": \"{macAddress}\",\n" +
                $"\"ProcModel\": \"{islemci}\",\n" +
                $"\"Username\": \"{userName}\",\n" +
                $"\"Drives\": [\n" +
                $"{driveInfo}" +
                $"],\n" +
                $"\"LastIpAddress\": \"{lastIpAddress}\",\n" +
                $"\"DateChanged\": \"{(DateTime.Now).ToString()}\"\n" +
                "}";




        }
        private async Task EnvanterBilgileriniGonder(string veri)
        {
            var content = new StringContent(veri, Encoding.UTF8, "application/json");

            Logger($"Sunucuya({_serverUrl}) gönderilmeye calisiliyor:\n{veri}");

            try
            {
                var response = await _httpClient.PostAsync(_serverUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Logger($"-------------------------------------\n{veri}" + "\nSunucuya basariyla gonderildi." + "\n-------------------------------------");
                }
                else
                {
                    Logger($"Sunucuya gönderilemedi. Hata kodu: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger($"HATA: {ex.Message}");
                Logger($"Inner Exception: {ex.InnerException?.Message}");
                Logger($"Stack Trace: {ex.StackTrace}");
            }
        }

        private static void Logger(string mesaj)
        {
            string dosyaYolu = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(dosyaYolu))
            {
                Directory.CreateDirectory(dosyaYolu);
            }

            string textYolu = dosyaYolu + "\\Log.txt";

            if (!File.Exists(textYolu))
            {
                using (StreamWriter sw = File.CreateText(textYolu))
                {
                    sw.WriteLine(mesaj);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(textYolu))
                {
                    sw.WriteLine(mesaj);
                }
            }
        }
        private static string ServerURLfromXML(string xmlFilePath)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlFilePath);

            XmlNode node = xmlDoc.SelectSingleNode("/config/serverIp");
            return node?.InnerText ?? "Bulunamadi";
        }

    }
}
