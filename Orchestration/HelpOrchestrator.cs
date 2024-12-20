using MSFSPopoutPanelManager.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MSFSPopoutPanelManager.Orchestration
{
    public class HelpOrchestrator : ObservableObject
    {
        public void OpenGettingStarted()
        {
            Process.Start(new ProcessStartInfo("https://github.com/hawkeye-stan/msfs-popout-panel-manager/blob/master/GETTING_STARTED.md") { UseShellExecute = true });
        }

        public void OpenUserGuide()
        {
            Process.Start(new ProcessStartInfo("https://github.com/hawkeye-stan/msfs-popout-panel-manager#msfs-pop-out-panel-manager") { UseShellExecute = true });
        }

        public void OpenLicense()
        {
            Process.Start("notepad.exe", "LICENSE");
        }

        public void OpenVersionInfo()
        {
            Process.Start("notepad.exe", "VERSION.md");
        }

        public void DownloadVccLibrary()
        {
            var target = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
            try
            {
                var psi = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = target
                };

                Process.Start(psi);
            }
            catch
            {
                // ignored
            }
        }

        public void DeleteAppCache()
        {
            var appLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var srcPath = Path.Combine(appLocal, @"temp\.net\MSFS Pop Out Panel Manager 2020");

            try
            {
                var currentAppPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location);

                if (currentAppPath == null)
                    throw new ApplicationException("Unable to determine POPM application path.");

                var dir = new DirectoryInfo(srcPath);
                var subDirs = dir.GetDirectories();

                foreach (var subDir in subDirs)
                {
                    if (subDir.FullName.ToLower().Trim() != currentAppPath.FullName.ToLower().Trim())
                    {
                        Directory.Delete(subDir.FullName, true);
                    }
                }
            }
            catch (Exception ex) 
            {
                FileLogger.WriteLog("Delete app cache exception: " + ex.Message, StatusMessageType.Error);
            }
        }
    }
}
