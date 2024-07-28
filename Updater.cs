using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;

namespace vividstasis_Text_Editor
{
    internal class Updater
    {
        static Loading downloadDialogue;
        public static bool CheckForUpdates()
        {
            // this is now drm
            // if it is removed, your warranty is void
            //can u not speak anymore
            // Cejkwct freo  putaptse
            // Cjkersct forep uptdates
            // Chjecsk for up traetse
            // Cjkerct fgo[r uprtastes]
            // Chjkecst fore syuptdae
            // Chkect fro upaters
            //
            // ^ very (real)
            int versionCode = GetLatestVersion().versionCode;
            int fileVersion = Int32.Parse(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            return versionCode > fileVersion;
        }
         static VersionInfo GetLatestVersion()
        {
            Uri json = new Uri("https://raw.githubusercontent.com/MichaelEpicA/vividstasis-Text-Editor/main/version.json");
            HttpWebRequest jsonRequest = WebRequest.CreateHttp(json);
            HttpWebResponse jsonResponse = (HttpWebResponse)jsonRequest.GetResponse();
            StreamReader reader = new StreamReader(jsonResponse.GetResponseStream());
            string jsonString = reader.ReadToEnd();
            reader.Dispose();
            VersionInfo info = JsonConvert.DeserializeObject<VersionInfo>(jsonString);
            return info;
        }
        public static void DownloadUpdate(Loading load)
        {
            VersionInfo info = GetLatestVersion();
            Uri githubRepo = new Uri($"https://api.github.com/repos/MichaelEpicA/vivid-stasis-Text-Editor/releases/v{info.version}");
            if (load == null)
            {
                downloadDialogue = new Loading();
            } else
            {
                downloadDialogue = load;
            }
            
            WebClient client = new WebClient();
            client.DownloadFileAsync(githubRepo, "vividstasis Text Editor-update.exe");
            downloadDialogue.EditLoadingTitle("Downloading...");
            downloadDialogue.EditLoadingMessage("Downloading...");
            downloadDialogue.Show();
            client.DownloadProgressChanged += Client_DownloadProgressChanged;
            client.DownloadFileCompleted += Client_DownloadFileCompleted;
        }

        public static void InstallUpdate()
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "vivid/stasis Text Editor-update.exe",
                Arguments = "-updatereboot",
                UseShellExecute = true, 
                CreateNoWindow = false
            };
            Process.Start(info);
            Environment.Exit(0);
        }

        private static void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            if(e.Error != null)
            {
                Exception ex = e.Error;
                File.WriteAllText(Path.Combine(Program.GetExecutableDirectory(), "downloaderror.txt"), (ex.ToString() + "\n" + ex.Message + "\n" + ex.StackTrace));
                MessageBox.Show("An error occured while downloading. This error has been logged in downloaderror.txt. Check your network connection and try again. If this still doesn't work, make an issue on the official github.","vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                downloadDialogue.Hide();
                return;
            }
            InstallUpdate();
        }

        private static void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadDialogue.EditLoadingMessage($"Downloading... ({e.BytesReceived}/{e.TotalBytesToReceive})");
        }

        struct VersionInfo
        {
            public string version;
            public int versionCode;
        }
    }
}
