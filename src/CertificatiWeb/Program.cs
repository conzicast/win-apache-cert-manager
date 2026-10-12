using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

[assembly: AssemblyTitle("Gestione Certificati Apache")]
[assembly: AssemblyDescription("Configurazione e rinnovo certificati HTTPS per Apache")]
[assembly: AssemblyCompany("2LAB")]
[assembly: AssemblyProduct("Gestione Certificati Apache")]
[assembly: AssemblyVersion("2.0.2.0")]
[assembly: AssemblyFileVersion("2.0.2.0")]
[assembly: AssemblyInformationalVersion("2.0.2")]

namespace CertificatiWeb
{
    internal static class AppInfo
    {
        public const string Version = "2.0.2";
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return SelfTests.Run();
            }
            if (args.Length == 1 && args[0].Equals("--renew-all", StringComparison.OrdinalIgnoreCase))
            {
                return RenewalRunner.RunAll() ? 0 : 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox domainsBox;
        private readonly TextBox siteFolderBox;
        private readonly TextBox certificateFolderBox;
        private readonly ComboBox apacheBox;
        private readonly TextBox openSslBox;
        private readonly TextBox le64Box;
        private readonly TextBox vhostsBox;
        private readonly TextBox statusBox;
        private readonly Button configureButton;
        private readonly Button refreshButton;
        private bool running;

        public MainForm()
        {
            Text = "Gestione Certificati Apache - v" + AppInfo.Version;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 735);
            MinimumSize = new Size(820, 690);
            Font = new Font("Segoe UI", 9F);

            Label title = Label("Nuovo sito HTTP + HTTPS", 24, 18);
            title.Font = new Font("Segoe UI Semibold", 15F);
            Controls.Add(title);

            Label help = Label(
                "Primo dominio = ServerName; gli altri = ServerAlias e SAN dello stesso certificato. Challenge ACME HTTP-01 sulla porta 80.",
                27,
                56);
            help.ForeColor = Color.FromArgb(0, 70, 140);
            Controls.Add(help);

            AddLabel("Domini, uno per riga", 84);
            domainsBox = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(30, 105),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(855, 58),
                Text = "elettro.zonaweb.it"
            };
            Controls.Add(domainsBox);

            AddLabel("Cartella completa del sito", 176);
            siteFolderBox = AddPathField(196, @"C:\server\xampp\htdocs\elettro", true);

            AddLabel("Cartella dove installare certificato e chiave", 232);
            certificateFolderBox = AddPathField(252, @"C:\server\xampp\apache\conf\certs\elettro", true);

            AddLabel("Processo Apache attivo da verificare e riavviare", 288);
            apacheBox = new ComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(30, 308),
                Size = new Size(735, 25)
            };
            apacheBox.SelectedIndexChanged += ApacheSelectionChanged;
            Controls.Add(apacheBox);
            refreshButton = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(775, 306),
                Size = new Size(110, 28),
                Text = "Aggiorna elenco"
            };
            refreshButton.Click += delegate { LoadApacheInstances(); };
            Controls.Add(refreshButton);

            AddLabel("Eseguibile OpenSSL", 344);
            openSslBox = AddPathField(364, @"C:\server\xampp\apache\bin\openssl.exe", false);

            AddLabel("Eseguibile ssl-le64", 400);
            le64Box = AddPathField(420, @"C:\server\ssl-le64\le64.exe", false);

            AddLabel("File httpd-vhosts.conf", 456);
            vhostsBox = AddPathField(476, @"C:\server\xampp\apache\conf\extra\httpd-vhosts.conf", false);

            configureButton = new Button
            {
                Location = new Point(30, 532),
                Size = new Size(255, 40),
                Text = "Configura HTTP e HTTPS"
            };
            configureButton.Click += ConfigureClick;
            Controls.Add(configureButton);

            AddLabel("Stato operazione", 586);
            statusBox = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F),
                Location = new Point(30, 606),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(855, 92)
            };
            Controls.Add(statusBox);

            Label version = Label("Versione " + AppInfo.Version + " - Windows 7 x64 SP1 / Windows 10 / Windows 11", 560, 708);
            version.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            version.ForeColor = Color.DimGray;
            Controls.Add(version);

            Shown += delegate { LoadApacheInstances(); };
            FormClosing += ClosingForm;
        }

        private static Label Label(string text, int x, int y)
        {
            return new Label { AutoSize = true, Location = new Point(x, y), Text = text };
        }

        private void AddLabel(string text, int y)
        {
            Controls.Add(Label(text, 27, y));
        }

        private TextBox AddPathField(int y, string defaultValue, bool folder)
        {
            TextBox box = new TextBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(30, y),
                Size = new Size(735, 23),
                Text = defaultValue
            };
            Controls.Add(box);
            Button browse = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(775, y - 2),
                Size = new Size(110, 27),
                Text = "Sfoglia..."
            };
            browse.Click += delegate { Browse(box, folder); };
            Controls.Add(browse);
            return box;
        }

        private void Browse(TextBox box, bool folder)
        {
            if (folder)
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.SelectedPath;
                }
            }
            else
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.CheckFileExists = true;
                    dialog.Filter = "Eseguibili e configurazioni|*.exe;*.conf|Tutti i file|*.*";
                    if (File.Exists(box.Text)) dialog.FileName = box.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
                }
            }
        }

        private void LoadApacheInstances()
        {
            string selectedPath = apacheBox.SelectedItem is ApacheInstance
                ? ((ApacheInstance)apacheBox.SelectedItem).ExecutablePath
                : string.Empty;
            List<ApacheInstance> instances = ApacheDiscovery.FindActiveInstances();
            apacheBox.Items.Clear();
            foreach (ApacheInstance instance in instances) apacheBox.Items.Add(instance);
            int index = instances.FindIndex(item => item.ExecutablePath.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) apacheBox.SelectedIndex = index;
            else if (apacheBox.Items.Count > 0) apacheBox.SelectedIndex = 0;
            Report(instances.Count == 0
                ? "Nessun processo httpd.exe attivo rilevato."
                : string.Format("Rilevate {0} istanze Apache attive.", instances.Count));
        }

        private void ApacheSelectionChanged(object sender, EventArgs e)
        {
            ApacheInstance instance = apacheBox.SelectedItem as ApacheInstance;
            if (instance == null) return;
            string apacheRoot = Path.GetDirectoryName(Path.GetDirectoryName(instance.ExecutablePath));
            string suggested = Path.Combine(apacheRoot, @"bin\openssl.exe");
            if (File.Exists(suggested)) openSslBox.Text = suggested;
        }

        private async void ConfigureClick(object sender, EventArgs e)
        {
            if (running) return;
            ApacheInstance apache = apacheBox.SelectedItem as ApacheInstance;
            if (apache == null)
            {
                MessageBox.Show(this, "Selezionare un processo Apache attivo.", "Dato mancante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ProvisioningRequest request = new ProvisioningRequest
            {
                Domains = InputParser.ParseDomains(domainsBox.Text),
                SiteFolder = siteFolderBox.Text.Trim(),
                CertificateFolder = certificateFolderBox.Text.Trim(),
                Apache = apache,
                OpenSslExecutable = openSslBox.Text.Trim(),
                Le64Executable = le64Box.Text.Trim(),
                VirtualHostsFile = vhostsBox.Text.Trim()
            };
            SetRunning(true);
            statusBox.Clear();
            OperationResult result = await Task.Factory.StartNew(() => new SiteProvisioner().Configure(request, Report));
            SetRunning(false);
            MessageBox.Show(
                this,
                result.Message,
                "Gestione Certificati",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void SetRunning(bool value)
        {
            running = value;
            configureButton.Enabled = !value;
            refreshButton.Enabled = !value;
        }

        private void Report(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Report), message);
                return;
            }
            statusBox.AppendText(string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine));
        }

        private void ClosingForm(object sender, FormClosingEventArgs e)
        {
            if (!running) return;
            e.Cancel = true;
            MessageBox.Show(this, "Attendere la conclusione dell'operazione.");
        }
    }

    internal static class ApacheDiscovery
    {
        public static List<ApacheInstance> FindActiveInstances()
        {
            Dictionary<int, string> services = new Dictionary<int, string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, ProcessId FROM Win32_Service WHERE State='Running'"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        int pid = Convert.ToInt32((uint)item["ProcessId"]);
                        if (pid > 0) services[pid] = Convert.ToString(item["Name"]);
                    }
                }
            }
            catch { }

            List<ApacheInstance> discovered = new List<ApacheInstance>();
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Process process in Process.GetProcessesByName("httpd"))
            {
                try
                {
                    string path = process.MainModule.FileName;
                    string service;
                    services.TryGetValue(process.Id, out service);
                    string key = path + "|" + (service ?? string.Empty);
                    if (unique.Add(key)) discovered.Add(new ApacheInstance(process.Id, path, service));
                }
                catch { }
            }
            List<ApacheInstance> results = new List<ApacheInstance>();
            foreach (IGrouping<string, ApacheInstance> group in discovered.GroupBy(
                item => item.ExecutablePath,
                StringComparer.OrdinalIgnoreCase))
            {
                List<ApacheInstance> serviceInstances = group
                    .Where(item => !string.IsNullOrWhiteSpace(item.ServiceName))
                    .ToList();
                if (serviceInstances.Count > 0)
                {
                    results.AddRange(serviceInstances);
                }
                else
                {
                    results.Add(group.OrderBy(item => item.ProcessId).First());
                }
            }
            return results.OrderBy(item => item.ExecutablePath).ThenBy(item => item.ServiceName).ToList();
        }
    }

    internal sealed class SiteProvisioner
    {
        private const int TimeoutMilliseconds = 900000;
        private readonly Encoding utf8 = new UTF8Encoding(false);
        private Action<string> reporter;
        private string operationLog;
        private string commandLog;

        public OperationResult Configure(ProvisioningRequest request, Action<string> statusReporter)
        {
            reporter = statusReporter;
            FileStream lockStream = null;
            string lockFile = string.Empty;
            try
            {
                RequestValidator.Validate(request);
                string leFolder = Path.GetDirectoryName(request.Le64Executable);
                string logFolder = Path.Combine(leFolder, "log");
                Directory.CreateDirectory(logFolder);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                operationLog = Path.Combine(logFolder, "gestione_" + stamp + ".log");
                commandLog = Path.Combine(logFolder, "comandi_" + stamp + ".log");
                lockFile = Path.Combine(leFolder, "gestione-certificati.lock");
                lockStream = new FileStream(lockFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                return ConfigureProtected(request, stamp);
            }
            catch (Exception exception)
            {
                Report("ERRORE: " + exception.Message);
                return OperationResult.Fail(exception.Message, operationLog);
            }
            finally
            {
                if (lockStream != null) lockStream.Dispose();
                try { if (!string.IsNullOrEmpty(lockFile) && File.Exists(lockFile)) File.Delete(lockFile); } catch { }
            }
        }

        private OperationResult ConfigureProtected(ProvisioningRequest request, string stamp)
        {
            string primary = request.Domains[0];
            string identifier = InputParser.Identifier(primary);
            string certPath = Path.Combine(request.CertificateFolder, "certificate_" + identifier + ".crt");
            string keyPath = Path.Combine(request.CertificateFolder, "private_" + identifier + ".key");
            string leFolder = Path.GetDirectoryName(request.Le64Executable);
            string workCert = Path.Combine(leFolder, "certificate_" + identifier + ".crt");
            string workKey = Path.Combine(leFolder, identifier + ".key");
            string accountKey = Path.Combine(leFolder, "account_" + identifier + ".key");
            string csr = Path.Combine(leFolder, identifier + ".csr");
            string challenge = Path.Combine(request.SiteFolder, @".well-known\acme-challenge");
            string backup = Path.Combine(request.CertificateFolder, "_backup", stamp);
            string vhostsBackup = Path.Combine(backup, "httpd-vhosts.conf");
            string certBackup = Path.Combine(backup, "certificate_precedente.crt");
            string keyBackup = Path.Combine(backup, "private_precedente.key");
            bool certExisted = File.Exists(certPath);
            bool keyExisted = File.Exists(keyPath);
            bool configChanged = false;

            string configuration = File.ReadAllText(request.VirtualHostsFile);
            foreach (string domain in request.Domains)
            {
                if (VirtualHosts.ContainsDomain(configuration, domain))
                    throw new InvalidOperationException("Dominio già presente nei VirtualHost: " + domain);
            }

            Directory.CreateDirectory(request.CertificateFolder);
            Directory.CreateDirectory(backup);
            Directory.CreateDirectory(challenge);
            File.Copy(request.VirtualHostsFile, vhostsBackup, true);
            if (certExisted) File.Copy(certPath, certBackup, true);
            if (keyExisted) File.Copy(keyPath, keyBackup, true);

            try
            {
                Report("Controllo configurazione Apache corrente...");
                ApacheCommands.Test(request, Run);
                File.AppendAllText(
                    request.VirtualHostsFile,
                    Environment.NewLine + VirtualHosts.BuildHttp(request.Domains, request.SiteFolder, identifier) + Environment.NewLine,
                    utf8);
                configChanged = true;
                Report("VirtualHost HTTP aggiunto; riavvio Apache...");
                ApacheCommands.Restart(request, Run);

                Report("Apache HTTP attivo. Avvio challenge sulla porta 80...");
                Issue(request, accountKey, csr, workKey, workCert, challenge);
                ValidateCertificate(request, workCert, workKey);
                File.Copy(workCert, certPath, true);
                File.Copy(workKey, keyPath, true);

                File.AppendAllText(
                    request.VirtualHostsFile,
                    Environment.NewLine + VirtualHosts.BuildHttps(
                        request.Domains, request.SiteFolder, identifier, certPath, keyPath) + Environment.NewLine,
                    utf8);
                Report("VirtualHost HTTPS aggiunto; verifica e riavvio Apache...");
                ApacheCommands.Restart(request, Run);
                RenewalProfiles.Upsert(RenewalProfile.FromRequest(request, certPath, keyPath));
                Report("Configurazione completata; profilo di rinnovo salvato.");
                return OperationResult.Ok("Sito configurato in HTTP e HTTPS per: " + string.Join(", ", request.Domains.ToArray()));
            }
            catch (Exception exception)
            {
                Report("Errore; ripristino in corso...");
                try
                {
                    if (configChanged) File.Copy(vhostsBackup, request.VirtualHostsFile, true);
                    Restore(certPath, certBackup, certExisted);
                    Restore(keyPath, keyBackup, keyExisted);
                    if (configChanged) ApacheCommands.Restart(request, Run);
                    Report("Ripristino completato.");
                }
                catch (Exception rollback)
                {
                    throw new InvalidOperationException(
                        exception.Message + Environment.NewLine + "Ripristino non completato: " + rollback.Message,
                        exception);
                }
                throw;
            }
        }

        private void Issue(
            ProvisioningRequest request,
            string accountKey,
            string csr,
            string workKey,
            string workCert,
            string challenge)
        {
            List<string> args = new List<string>
            {
                "--key", accountKey, "--csr", csr, "--csr-key", workKey, "--crt", workCert,
                "--domains", string.Join(",", request.Domains.ToArray()), "--generate-missing", "--live", "--unlink",
                "--path", Paths.Apache(challenge), "--issue-code", "100"
            };
            if (File.Exists(workCert)) args.AddRange(new[] { "--renew", "20" });
            CommandResult result = Run(request.Le64Executable, CommandLine.Join(args));
            if (result.ExitCode != 100 && result.Combined.IndexOf("Too early for renewal", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Emissione certificato fallita, codice " + result.ExitCode + ".");
            RequestValidator.FileExists(workCert, "Certificato generato");
            RequestValidator.FileExists(workKey, "Chiave privata generata");
        }

        private void ValidateCertificate(ProvisioningRequest request, string cert, string key)
        {
            CertificateInspector.ValidateDomainsAndDates(cert, request.Domains);
            CommandResult certKey = Run(request.OpenSslExecutable, CommandLine.Join(new[] { "x509", "-in", cert, "-pubkey", "-noout" }));
            CommandResult privateKey = Run(request.OpenSslExecutable, CommandLine.Join(new[] { "pkey", "-in", key, "-pubout" }));
            if (certKey.ExitCode != 0 || privateKey.ExitCode != 0 || certKey.Output.Trim() != privateKey.Output.Trim())
                throw new InvalidOperationException("Certificato e chiave privata non corrispondono.");
        }

        private CommandResult Run(string executable, string arguments)
        {
            CommandResult result = ProcessRunner.Run(executable, arguments, TimeoutMilliseconds);
            StringBuilder log = new StringBuilder();
            log.AppendLine("------------------------------------------------------------");
            log.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            log.AppendLine(executable + " " + arguments);
            log.AppendLine("ExitCode=" + result.ExitCode);
            log.AppendLine(result.Output);
            log.AppendLine(result.Error);
            File.AppendAllText(commandLog, log.ToString(), utf8);
            return result;
        }

        private void Report(string message)
        {
            if (reporter != null) reporter(message);
            if (!string.IsNullOrEmpty(operationLog))
                File.AppendAllText(operationLog, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + Environment.NewLine, utf8);
        }

        private static void Restore(string destination, string backup, bool existed)
        {
            if (existed && File.Exists(backup)) File.Copy(backup, destination, true);
            else if (!existed && File.Exists(destination)) File.Delete(destination);
        }
    }

    internal static class ApacheCommands
    {
        public static void Test(ProvisioningRequest request, Func<string, string, CommandResult> run)
        {
            List<string> args = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Apache.ServiceName)) args.AddRange(new[] { "-n", request.Apache.ServiceName });
            args.Add("-t");
            if (run(request.Apache.ExecutablePath, CommandLine.Join(args)).ExitCode != 0)
                throw new InvalidOperationException("Configurazione Apache non valida.");
        }

        public static void Restart(ProvisioningRequest request, Func<string, string, CommandResult> run)
        {
            Test(request, run);
            List<string> args = new List<string> { "-k", "restart" };
            if (!string.IsNullOrWhiteSpace(request.Apache.ServiceName)) args.AddRange(new[] { "-n", request.Apache.ServiceName });
            if (run(request.Apache.ExecutablePath, CommandLine.Join(args)).ExitCode != 0)
                throw new InvalidOperationException("Riavvio Apache fallito.");
        }
    }

    internal static class VirtualHosts
    {
        public static bool ContainsDomain(string configuration, string domain)
        {
            foreach (Match line in Regex.Matches(configuration, @"(?im)^\s*(?:ServerName|ServerAlias)\s+([^#\r\n]+)"))
            {
                string[] names = line.Groups[1].Value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (names.Any(name => name.Equals(domain, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }

        public static string BuildHttp(List<string> domains, string folder, string logName)
        {
            return Build(domains, folder, logName, false, null, null);
        }

        public static string BuildHttps(List<string> domains, string folder, string logName, string cert, string key)
        {
            return Build(domains, folder, logName, true, cert, key);
        }

        private static string Build(List<string> domains, string folder, string logName, bool https, string cert, string key)
        {
            string root = Paths.Apache(folder);
            string suffix = https ? "-ssl" : string.Empty;
            StringBuilder text = new StringBuilder();
            text.AppendLine("# BEGIN GESTIONE-CERTIFICATI " + (https ? "HTTPS " : "HTTP ") + domains[0]);
            if (!https) text.AppendLine("# HTTP disponibile per la challenge ACME HTTP-01.");
            text.AppendLine("<VirtualHost *:" + (https ? "443" : "80") + ">");
            text.AppendLine("    ServerName " + domains[0]);
            if (domains.Count > 1) text.AppendLine("    ServerAlias " + string.Join(" ", domains.Skip(1).ToArray()));
            text.AppendLine("    DocumentRoot \"" + root + "\"");
            text.AppendLine("    DirectoryIndex index.php index.html");
            text.AppendLine();
            text.AppendLine("    <Directory \"" + root + "\">");
            text.AppendLine("        Options -Indexes +FollowSymLinks");
            text.AppendLine("        AllowOverride All");
            text.AppendLine("        Require all granted");
            text.AppendLine("    </Directory>");
            if (https)
            {
                text.AppendLine();
                text.AppendLine("    SSLEngine on");
                text.AppendLine("    SSLCertificateFile \"" + Paths.Apache(cert) + "\"");
                text.AppendLine("    SSLCertificateKeyFile \"" + Paths.Apache(key) + "\"");
            }
            text.AppendLine();
            text.AppendLine("    ErrorLog \"logs/" + logName + suffix + "-error.log\"");
            text.AppendLine("    CustomLog \"logs/" + logName + suffix + "-access.log\" combined");
            text.AppendLine("</VirtualHost>");
            text.Append("# END GESTIONE-CERTIFICATI " + (https ? "HTTPS " : "HTTP ") + domains[0]);
            return text.ToString();
        }
    }

    internal static class RequestValidator
    {
        public static void Validate(ProvisioningRequest request)
        {
            if (request.Domains == null || request.Domains.Count == 0) throw new InvalidOperationException("Inserire almeno un dominio.");
            foreach (string domain in request.Domains)
                if (!InputParser.ValidDomain(domain)) throw new InvalidOperationException("Dominio non valido: " + domain);
            DirectoryExists(request.SiteFolder, "Cartella sito");
            if (string.IsNullOrWhiteSpace(request.CertificateFolder)) throw new InvalidOperationException("Selezionare la cartella certificati.");
            FileExists(request.Apache.ExecutablePath, "Eseguibile Apache");
            FileExists(request.OpenSslExecutable, "Eseguibile OpenSSL");
            FileExists(request.Le64Executable, "Eseguibile ssl-le64");
            FileExists(request.VirtualHostsFile, "File VirtualHost");
        }

        public static void FileExists(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException(description + " non trovato: " + path, path);
        }

        private static void DirectoryExists(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException(description + " non trovata: " + path);
        }
    }

    internal static class InputParser
    {
        private static readonly Regex Domain = new Regex(
            @"^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<string> ParseDomains(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { '\r', '\n', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool ValidDomain(string value) { return Domain.IsMatch(value ?? string.Empty); }
        public static string Identifier(string value) { return Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]", string.Empty); }
    }

    internal static class RenewalProfiles
    {
        public static string PathName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GestioneCertificati.profiles.xml"); } }

        public static List<RenewalProfile> Load()
        {
            if (!File.Exists(PathName)) return new List<RenewalProfile>();
            XDocument document = XDocument.Load(PathName);
            return document.Root.Elements("site").Select(RenewalProfile.FromXml).ToList();
        }

        public static void Upsert(RenewalProfile profile)
        {
            List<RenewalProfile> profiles = Load();
            profiles.RemoveAll(item => item.PrimaryDomain.Equals(profile.PrimaryDomain, StringComparison.OrdinalIgnoreCase));
            profiles.Add(profile);
            XDocument document = new XDocument(
                new XElement("renewalProfiles", new XAttribute("version", "2"), profiles.OrderBy(item => item.PrimaryDomain).Select(item => item.ToXml())));
            string temporary = PathName + ".tmp";
            document.Save(temporary);
            File.Copy(temporary, PathName, true);
            File.Delete(temporary);
        }
    }

    internal static class RenewalRunner
    {
        public static bool RunAll()
        {
            try
            {
                List<RenewalProfile> profiles = RenewalProfiles.Load();
                if (profiles.Count == 0) return false;
                bool success = true;
                foreach (RenewalProfile profile in profiles)
                {
                    try { Renew(profile); }
                    catch { success = false; }
                }
                return success;
            }
            catch { return false; }
        }

        private static void Renew(RenewalProfile profile)
        {
            ProvisioningRequest request = profile.Request();
            RequestValidator.Validate(request);
            string id = InputParser.Identifier(profile.PrimaryDomain);
            string leFolder = Path.GetDirectoryName(request.Le64Executable);
            string workCert = Path.Combine(leFolder, "certificate_" + id + ".crt");
            string workKey = Path.Combine(leFolder, id + ".key");
            string challenge = Path.Combine(request.SiteFolder, @".well-known\acme-challenge");
            Directory.CreateDirectory(challenge);
            string args = CommandLine.Join(new[]
            {
                "--key", Path.Combine(leFolder, "account_" + id + ".key"),
                "--csr", Path.Combine(leFolder, id + ".csr"),
                "--csr-key", workKey, "--crt", workCert,
                "--domains", string.Join(",", request.Domains.ToArray()), "--generate-missing", "--live", "--unlink",
                "--path", Paths.Apache(challenge), "--issue-code", "100", "--renew", "20"
            });
            CommandResult result = ProcessRunner.Run(request.Le64Executable, args, 900000);
            if (result.ExitCode != 100 && result.Combined.IndexOf("Too early for renewal", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (result.ExitCode != 100) throw new InvalidOperationException("Rinnovo fallito.");

            CertificateInspector.ValidateDomainsAndDates(workCert, request.Domains);
            CommandResult certificateKey = ProcessRunner.RunDefault(
                request.OpenSslExecutable,
                CommandLine.Join(new[] { "x509", "-in", workCert, "-pubkey", "-noout" }));
            CommandResult privateKey = ProcessRunner.RunDefault(
                request.OpenSslExecutable,
                CommandLine.Join(new[] { "pkey", "-in", workKey, "-pubout" }));
            if (certificateKey.ExitCode != 0 || privateKey.ExitCode != 0 ||
                certificateKey.Output.Trim() != privateKey.Output.Trim())
                throw new InvalidOperationException("Certificato rinnovato e chiave privata non corrispondono.");

            string backup = Path.Combine(request.CertificateFolder, "_backup_rinnovi", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backup);
            string certBackup = Path.Combine(backup, Path.GetFileName(profile.CertificatePath));
            string keyBackup = Path.Combine(backup, Path.GetFileName(profile.PrivateKeyPath));
            File.Copy(profile.CertificatePath, certBackup, true);
            File.Copy(profile.PrivateKeyPath, keyBackup, true);
            try
            {
                File.Copy(workCert, profile.CertificatePath, true);
                File.Copy(workKey, profile.PrivateKeyPath, true);
                ApacheCommands.Restart(request, ProcessRunner.RunDefault);
            }
            catch
            {
                File.Copy(certBackup, profile.CertificatePath, true);
                File.Copy(keyBackup, profile.PrivateKeyPath, true);
                try { ApacheCommands.Restart(request, ProcessRunner.RunDefault); } catch { }
                throw;
            }
        }
    }

    internal static class ProcessRunner
    {
        public static CommandResult RunDefault(string executable, string arguments) { return Run(executable, arguments, 900000); }

        public static CommandResult Run(string executable, string arguments, int timeout)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
            using (Process process = new Process())
            {
                process.StartInfo = info;
                process.Start();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeout))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Comando scaduto: " + executable);
                }
                Task.WaitAll(output, error);
                return new CommandResult(process.ExitCode, output.Result, error.Result);
            }
        }
    }

    internal static class CommandLine
    {
        public static string Join(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(item => "\"" + (item ?? string.Empty).Replace("\"", "\\\"") + "\"").ToArray());
        }
    }

    internal static class Paths
    {
        public static string Apache(string value) { return value.Replace('\\', '/'); }
    }

    internal static class CertificateInspector
    {
        private const string SubjectAlternativeNameOid = "2.5.29.17";

        public static void ValidateDomainsAndDates(string certificatePath, IEnumerable<string> requiredDomains)
        {
            using (X509Certificate2 certificate = new X509Certificate2(certificatePath))
            {
                DateTime now = DateTime.Now;
                if (now < certificate.NotBefore || now > certificate.NotAfter)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "Certificato non valido alla data corrente (validità: {0:g} - {1:g}).",
                            certificate.NotBefore,
                            certificate.NotAfter));
                }

                List<string> certificateDomains = ReadDnsNames(certificate);
                if (certificateDomains.Count == 0)
                {
                    string commonName = certificate.GetNameInfo(X509NameType.DnsName, false);
                    if (!string.IsNullOrWhiteSpace(commonName)) certificateDomains.Add(commonName);
                }

                foreach (string domain in requiredDomains)
                {
                    if (!certificateDomains.Any(item => MatchesDomain(item, domain)))
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "Il certificato non contiene {0}. Nomi presenti: {1}",
                                domain,
                                certificateDomains.Count == 0 ? "nessuno" : string.Join(", ", certificateDomains.ToArray())));
                    }
                }
            }
        }

        private static List<string> ReadDnsNames(X509Certificate2 certificate)
        {
            foreach (X509Extension extension in certificate.Extensions)
            {
                if (extension.Oid != null && extension.Oid.Value == SubjectAlternativeNameOid)
                {
                    return ReadDnsNamesFromDer(extension.RawData);
                }
            }
            return new List<string>();
        }

        internal static List<string> ReadDnsNamesFromDer(byte[] rawData)
        {
            List<string> names = new List<string>();
            if (rawData == null || rawData.Length < 2) return names;

            int offset = 0;
            if (rawData[offset++] != 0x30) return names;
            int sequenceLength = ReadDerLength(rawData, ref offset);
            int end = Math.Min(rawData.Length, offset + sequenceLength);
            while (offset < end)
            {
                byte tag = rawData[offset++];
                int valueLength = ReadDerLength(rawData, ref offset);
                if (valueLength < 0 || offset + valueLength > end) break;
                if (tag == 0x82)
                {
                    names.Add(Encoding.ASCII.GetString(rawData, offset, valueLength).ToLowerInvariant());
                }
                offset += valueLength;
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int ReadDerLength(byte[] data, ref int offset)
        {
            if (offset >= data.Length) return -1;
            int first = data[offset++];
            if ((first & 0x80) == 0) return first;
            int byteCount = first & 0x7F;
            if (byteCount == 0 || byteCount > 4 || offset + byteCount > data.Length) return -1;
            int length = 0;
            for (int index = 0; index < byteCount; index++)
            {
                length = (length << 8) | data[offset++];
            }
            return length;
        }

        private static bool MatchesDomain(string certificateName, string requestedDomain)
        {
            string expected = (certificateName ?? string.Empty).Trim().ToLowerInvariant();
            string actual = (requestedDomain ?? string.Empty).Trim().ToLowerInvariant();
            if (expected == actual) return true;
            if (!expected.StartsWith("*.", StringComparison.Ordinal)) return false;
            string suffix = expected.Substring(1);
            if (!actual.EndsWith(suffix, StringComparison.Ordinal)) return false;
            string prefix = actual.Substring(0, actual.Length - suffix.Length);
            return prefix.Length > 0 && prefix.IndexOf('.') < 0;
        }
    }

    internal static class SelfTests
    {
        public static int Run()
        {
            try
            {
                List<string> domains = InputParser.ParseDomains("www.example.it\r\nexample.it, www.example.it");
                if (domains.Count != 2 || domains[0] != "www.example.it") return 10;
                if (!InputParser.ValidDomain("elettro.zonaweb.it") || InputParser.ValidDomain("dominio non valido")) return 11;
                string http = VirtualHosts.BuildHttp(domains, @"C:\server\xampp\htdocs\sito", "sito");
                if (!http.Contains("<VirtualHost *:80>") || !http.Contains("ServerAlias example.it")) return 12;
                string https = VirtualHosts.BuildHttps(domains, @"C:\sito", "sito", @"C:\certs\c.crt", @"C:\certs\k.key");
                if (!https.Contains("<VirtualHost *:443>") || !https.Contains("C:/certs/c.crt")) return 13;
                if (!VirtualHosts.ContainsDomain(http, "example.it") || VirtualHosts.ContainsDomain(http, "other.example.it")) return 14;
                byte[] san = BuildSanDer(new[] { "zwmail.duckdns.org", "mail.example.it" });
                List<string> parsedNames = CertificateInspector.ReadDnsNamesFromDer(san);
                if (parsedNames.Count != 2 || !parsedNames.Contains("zwmail.duckdns.org")) return 15;
                return 0;
            }
            catch { return 99; }
        }

        private static byte[] BuildSanDer(IEnumerable<string> names)
        {
            List<byte> content = new List<byte>();
            foreach (string name in names)
            {
                byte[] value = Encoding.ASCII.GetBytes(name);
                content.Add(0x82);
                content.Add((byte)value.Length);
                content.AddRange(value);
            }
            List<byte> result = new List<byte> { 0x30, (byte)content.Count };
            result.AddRange(content);
            return result.ToArray();
        }
    }

    internal sealed class ProvisioningRequest
    {
        public List<string> Domains { get; set; }
        public string SiteFolder { get; set; }
        public string CertificateFolder { get; set; }
        public ApacheInstance Apache { get; set; }
        public string OpenSslExecutable { get; set; }
        public string Le64Executable { get; set; }
        public string VirtualHostsFile { get; set; }
    }

    internal sealed class ApacheInstance
    {
        public ApacheInstance(int pid, string executable, string service)
        {
            ProcessId = pid;
            ExecutablePath = executable;
            ServiceName = service ?? string.Empty;
        }
        public int ProcessId { get; private set; }
        public string ExecutablePath { get; private set; }
        public string ServiceName { get; private set; }
        public override string ToString()
        {
            return string.Format("PID {0} | {1} | {2}", ProcessId,
                string.IsNullOrEmpty(ServiceName) ? "nessun servizio rilevato" : "servizio " + ServiceName,
                ExecutablePath);
        }
    }

    internal sealed class RenewalProfile
    {
        public string PrimaryDomain { get; set; }
        public List<string> Domains { get; set; }
        public string SiteFolder { get; set; }
        public string CertificateFolder { get; set; }
        public string CertificatePath { get; set; }
        public string PrivateKeyPath { get; set; }
        public string ApacheExecutable { get; set; }
        public string ApacheService { get; set; }
        public string OpenSslExecutable { get; set; }
        public string Le64Executable { get; set; }
        public string VirtualHostsFile { get; set; }

        public static RenewalProfile FromRequest(ProvisioningRequest request, string cert, string key)
        {
            return new RenewalProfile
            {
                PrimaryDomain = request.Domains[0], Domains = new List<string>(request.Domains),
                SiteFolder = request.SiteFolder, CertificateFolder = request.CertificateFolder,
                CertificatePath = cert, PrivateKeyPath = key, ApacheExecutable = request.Apache.ExecutablePath,
                ApacheService = request.Apache.ServiceName, OpenSslExecutable = request.OpenSslExecutable,
                Le64Executable = request.Le64Executable, VirtualHostsFile = request.VirtualHostsFile
            };
        }

        public ProvisioningRequest Request()
        {
            return new ProvisioningRequest
            {
                Domains = new List<string>(Domains), SiteFolder = SiteFolder, CertificateFolder = CertificateFolder,
                Apache = new ApacheInstance(0, ApacheExecutable, ApacheService), OpenSslExecutable = OpenSslExecutable,
                Le64Executable = Le64Executable, VirtualHostsFile = VirtualHostsFile
            };
        }

        public XElement ToXml()
        {
            return new XElement("site", new XAttribute("primaryDomain", PrimaryDomain),
                new XElement("domains", Domains.Select(item => new XElement("domain", item))),
                new XElement("siteFolder", SiteFolder), new XElement("certificateFolder", CertificateFolder),
                new XElement("certificatePath", CertificatePath), new XElement("privateKeyPath", PrivateKeyPath),
                new XElement("apacheExecutable", ApacheExecutable), new XElement("apacheService", ApacheService ?? string.Empty),
                new XElement("openSslExecutable", OpenSslExecutable), new XElement("le64Executable", Le64Executable),
                new XElement("virtualHostsFile", VirtualHostsFile));
        }

        public static RenewalProfile FromXml(XElement node)
        {
            return new RenewalProfile
            {
                PrimaryDomain = (string)node.Attribute("primaryDomain"),
                Domains = node.Element("domains").Elements("domain").Select(item => item.Value).ToList(),
                SiteFolder = (string)node.Element("siteFolder"), CertificateFolder = (string)node.Element("certificateFolder"),
                CertificatePath = (string)node.Element("certificatePath"), PrivateKeyPath = (string)node.Element("privateKeyPath"),
                ApacheExecutable = (string)node.Element("apacheExecutable"), ApacheService = (string)node.Element("apacheService"),
                OpenSslExecutable = (string)node.Element("openSslExecutable"), Le64Executable = (string)node.Element("le64Executable"),
                VirtualHostsFile = (string)node.Element("virtualHostsFile")
            };
        }
    }

    internal sealed class CommandResult
    {
        public CommandResult(int code, string output, string error) { ExitCode = code; Output = output ?? string.Empty; Error = error ?? string.Empty; }
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public string Error { get; private set; }
        public string Combined { get { return Output + Environment.NewLine + Error; } }
    }

    internal sealed class OperationResult
    {
        private OperationResult(bool success, string message) { Success = success; Message = message; }
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public static OperationResult Ok(string message) { return new OperationResult(true, message); }
        public static OperationResult Fail(string message, string log)
        {
            return new OperationResult(false, "Operazione non completata: " + message +
                (string.IsNullOrEmpty(log) ? string.Empty : Environment.NewLine + Environment.NewLine + "Log: " + log));
        }
    }
}
