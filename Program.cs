using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BasicThemer2
{
    static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string[] args = Environment.GetCommandLineArgs();
            if (args.Any(x => x.Contains("help")) || args.Any(x => x.Contains("?"))) {
                MessageBox.Show("showui: Show the UI on startup.\ndonthide: Don't hide the UI ever.\nhidetray: Hide the tray icon completely.\nnoadminalert: Don't ask for admin privileges.\nenablelogging: Enable logging on startup.\nnoautoupdchk: Disable automatic update check.\nhelp, ?: Show this message and exit.\nversion, ver: Show version number and exit.", "BasicThemer 2 Command-Line Arguments");
                return;
            }

            if (args.Any(x => x.Contains("ver")))
            {
                MessageBox.Show("Version " + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion, "BasicThemer 2");
                return;
            }

            Process adminInstance = null;
            if (!IsAdministrator())
            {
                EnsureDetourDllsExtracted();

                //We MUST inject into explorer while NOT elevated, otherwise we could pass elevation onto explorer...recursively... (big security no-no)
                if (true || MessageBox.Show("Restart explorer so BasicThemer2 can inject code into it? (PLACEHOLDER)", "BasicThemer 2", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var explorerPid = InjectExplorer();
                    try
                    {
                        _explorerProcess = Process.GetProcessById(explorerPid);
                        _explorerProcess.Exited += Explorer_Exited;
                    }
                    catch (Exception ex)
                    {
                        //?????
                    }
                }

                if (!args.Any(x => x.Contains("noadminalert")))
                {
                    if (false && MessageBox.Show("BasicThemer 2 requires administrator privileges in order to apply the basic theme to other programs which have administrator privileges. Relaunch as administrator?", "BasicThemer 2", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        adminInstance = RerunAsAdministrator();

                        if (adminInstance == null)
                        {
                            MessageBox.Show("Relaunch was aborted. Proceeding without Administrator privileges. Other applications running as administrator will not receive basic theme borders.", "BasicThemer 2");
                        }
                        else
                        {
                            //Keep the non-admin instance alive - we'll need it to restart explorer if it crashes later on
                            adminInstance.WaitForExit();
                            Environment.Exit(adminInstance.ExitCode);
                            return;
                        }
                    }
                }
            }


            Application.Run(new BasicThemer2());
        }


        static Process _explorerProcess = null;
        static int _explorerRestartRetryCount = 0;
        private static void Explorer_Exited(object sender, EventArgs e)
        {
            //Presumably explorer crashed, so we restart it ourselves and inject into it once again
            //TODO: Find out if this can be verified by exit code or something like that
            _explorerProcess.Exited -= Explorer_Exited;
            _explorerProcess = null;
            if (_explorerRestartRetryCount < 3)
            {
                InjectExplorer();
            }
            else
            {
                if (MessageBox.Show("Is explorer caught in a crash loop? (PLACEHOLDER)", "BasicThemer 2", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    MessageBox.Show("Aborting detours injection. Explorer should now start normally. (PLACEHOLDER)");
                }
                else
                    _explorerRestartRetryCount = -1;
            }
            _explorerRestartRetryCount++;
        }

        static string DetourDllsPath = @"C:\BasicThemer2\DetourDLLs"; //TODO: Don't hardcode

        static void EnsureDetourDllsExtracted()
        {
            if (!Directory.Exists(DetourDllsPath))
            {
                Directory.CreateDirectory(DetourDllsPath);
            }

            Extract6432("BasicThemer{0}.dll");
            Extract6432("Injector{0}.exe");   
        }

        static void Extract6432(string nameBase)
        {
            string file32path = Path.Combine(DetourDllsPath, string.Format(nameBase, "32"));
            string file64path = Path.Combine(DetourDllsPath, string.Format(nameBase, "64"));

            ExtractResource(Path.GetFileName(file32path), file32path);
            ExtractResource(Path.GetFileName(file64path), file64path);
        }

        static void ExtractResource(string resName, string outputPath)
        {
            var resStream = Assembly.GetEntryAssembly().GetManifestResourceStream($"BasicThemer2.{resName}");
            using (var reader = new BinaryReader(resStream))
            {
                /*using (FileStream outStream = new FileStream(outputPath, FileMode.OpenOrCreate)) // Say
                {
                    BinaryWriter writer = new BinaryWriter(outStream);*/
                    byte[] bytes = new byte[resStream.Length];
                    resStream.Read(bytes, 0, bytes.Length);
                    //outStream.wrt.Write(bytes);
                    File.WriteAllBytes(outputPath, bytes);
                //}
            }
        }

        static int InjectExplorer()
        {
            Process.Start("taskkill", "/f /im explorer.exe").WaitForExit();
            /*foreach (Process explProc in Process.GetProcessesByName("explorer"))
            {
                explProc.Close();
            }*/
            string injectorExePath = Path.Combine(DetourDllsPath, "Injector" + (Environment.Is64BitOperatingSystem ? "64" : "32") + ".exe");
            var process = Process.Start(injectorExePath);
            process.WaitForExit();
            return process.ExitCode;
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static Process RerunAsAdministrator()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 0)
                args = args.Skip(1).ToArray();

            string argString = string.Empty;
            foreach (string s in args)
                argString = argString + "\"" + s + "\" ";

            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                return Process.Start(new ProcessStartInfo(exeName, argString)
                {
                    Verb = "runas"
                });
            }
            catch (Win32Exception) 
            {
                return null;
            }
        }
    }
}
