using Newtonsoft.Json;
using System;
using System.IO;
using System.Management;
using System.Net.Http;
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
        string diskGB;
        string macAddress;
        string userName;
        string islemci;
        string model;
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

        protected override void OnStart(string[] args)
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
            EnvanterBilgileriniGonder(veri);
            timer.Interval = 1000 * 60 * 5;
            timer.Elapsed += new ElapsedEventHandler(TimerElapsed);
            timer.Enabled = true;
            timer.Start();
            timer2.Interval = 1000 * 60 * 60;
            timer2.Elapsed += new ElapsedEventHandler(Timer2Elapsed);
            timer2.Enabled = true;
            timer2.Start();



        }

        private void Timer2Elapsed(object source, ElapsedEventArgs e)
        {
            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\Log.txt");
        }

        protected override void OnStop()
        {
            Logger("Servis durdu." + DateTime.Now +"\n\n");
            timer.Stop();
        }
        private void TimerElapsed(object source, ElapsedEventArgs e)
        {
            Logger("Servis Çalışıyor." + DateTime.Now);
            string veri = EnvanterBilgileriniAl();
            EnvanterBilgileriniGonder(veri);
            if (DateTime.Now.Hour == 15 && DateTime.Now.Minute == 0)
            {
                EnvanterBilgileriniGonder(veri);
                Logger($"Log'a yazildi:\n {veri}");
            }
        }
        private string EnvanterBilgileriniAl()
        {
            ulong ramCapacity;
            ulong diskCapacity = 0;


            ManagementObjectSearcher biosSearcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (ManagementObject obj in biosSearcher.Get())
            {
                seriNo = obj["SerialNumber"].ToString();


            }

            ManagementObjectSearcher compSearcher = new ManagementObjectSearcher("SELECT Name, Model, TotalPhysicalMemory, UserName FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in compSearcher.Get())
            {
                computerName = obj["Name"].ToString();
                userName = obj["UserName"].ToString();
                userName = userName.Split('\\')[1];
                model = obj["Model"].ToString();
                ramCapacity = (ulong)obj["TotalPhysicalMemory"];
                ramGB = Math.Ceiling((decimal)(ramCapacity) / 1073741824).ToString("F2");

            }

            ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3");
            foreach (ManagementObject obj in diskSearcher.Get())
            {
                if (obj["Size"] != null)
                {
                    ulong diskSize = (ulong)obj["Size"];
                    decimal diskDecimal = (decimal)diskSize;
                    diskCapacity += (ulong)Math.Ceiling(diskDecimal);

                }
                diskGB = Math.Ceiling((decimal)(diskCapacity) / 1073741824).ToString("F2");
            }

            ManagementObjectSearcher macSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            foreach (ManagementObject obj in macSearcher.Get())
            {
                macAddress = obj["MacAddress"].ToString();

            }


            ManagementObjectSearcher procSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject obj in procSearcher.Get())
            {
                islemci = obj["Name"].ToString().Trim();

            }

            return $"SeriNo: {seriNo},\n" +
                $"CompModel: {model},\n" +
                $"CompName: {computerName},\n" +
                $"RAM: {ramGB},\n" +
                $"DiskGB: {diskGB},\n" +
                $"MAC: {macAddress},\n" +
                $"ProcModel: {islemci},\n" +
                $"Username: {userName},\n" +
                $"DateChanged: {(DateTime.Now).ToString()}\n";




        }
        private async Task EnvanterBilgileriniGonder(string veri)
        {
            var jsonData = JsonConvert.SerializeObject(new
            {
                SeriNo = seriNo,
                CompModel = model,
                CompName = computerName,
                RAM = ramGB,
                DiskGB = diskGB,
                MAC = macAddress,
                ProcModel = islemci,
                Username = userName,
                DateChanged = (DateTime.Now).ToString()
            });

            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            Logger($"Sunucuya({_serverUrl}) gönderilmeye calisiliyor:\n{veri}");

            try
            {
                var response = await _httpClient.PostAsync(_serverUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Logger($"Sunucuya gönderildi:\n {veri}");
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
