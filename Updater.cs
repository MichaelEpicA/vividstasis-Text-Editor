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
using System.IO.Compression;

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
            Uri githubRepo = new Uri($"https://api.github.com/repos/MichaelEpicA/vividstasis-Text-Editor/releases/tags/v{info.version}");
            if (load == null)
            {
                downloadDialogue = new Loading();
            } else
            {
                downloadDialogue = load;
            }
            HttpWebResponse response = null;
            try
            {
                HttpWebRequest httpWebRequest = WebRequest.CreateHttp(githubRepo);
                httpWebRequest.Accept = "application/vnd.github+json";
                httpWebRequest.Headers.Add("X-Github-Api-Version", "2022-11-28");
                httpWebRequest.UserAgent = "vivid/stasis Text Editor";
                response = (HttpWebResponse)httpWebRequest.GetResponse();
            } catch(WebException ex)
            {
                File.WriteAllText(Path.Combine(Program.GetExecutableDirectory(), "downloaderror.txt"), (ex.ToString() + "\n" + ex.Message + "\n" + ex.StackTrace));
                MessageBox.Show("An error occured while determining the version number. This error has been logged in downloaderror.txt. Check your network connection and try again. If this still doesn't work, make an issue on the official github.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if(response.StatusCode == HttpStatusCode.OK)
            {
                Uri downloadUrl = new Uri("https://google.com");
                string json;
                using(StreamReader reader = new StreamReader(response.GetResponseStream()))
                {json = reader.ReadToEnd(); }
                JArray array = (JArray)JObject.Parse(json)["assets"];
                foreach(JObject item in array)
                {
                    if (item["name"].ToString().Contains("Windows"))
                    {
                        downloadUrl = new Uri(item["browser_download_url"].ToString());
                        break;
                    }
                }
                WebClient client = new WebClient();
                client.Headers.Add(HttpRequestHeader.Accept, "application/vnd.github+json");
                client.Headers.Add("X-Github-Api-Version", "2022-11-28");
                client.DownloadFileAsync(downloadUrl, "vividstasis Text Editor-update.zip");
                downloadDialogue.EditLoadingTitle("Downloading...");
                downloadDialogue.EditLoadingMessage("Downloading...");
                downloadDialogue.Show();
                client.DownloadProgressChanged += Client_DownloadProgressChanged;
                client.DownloadFileCompleted += Client_DownloadFileCompleted;
            }
           
        }

        public static void InstallUpdate()
        {
            //ZipFile.ExtractToDirectory("vividstasis Text Editor-update.zip", "Update");
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "Update\\vividstasis Text Editor.exe",
                    Arguments = "-updatereboot",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                Process.Start(info);
            }
            catch(Exception e)
            {
                MessageBox.Show("Failed to launch the program.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                downloadDialogue.Hide();
                return;
            }
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
            downloadDialogue.EditLoadingMessage($"Downloading... ({e.BytesReceived/1000/1000}MBs/{e.TotalBytesToReceive/1000/1000}MBs)");
        }

        struct VersionInfo
        {
            public string version;
            public int versionCode;
        }
    }
}
