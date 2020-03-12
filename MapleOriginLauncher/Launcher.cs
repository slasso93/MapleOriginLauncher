using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MapleOriginLauncher
{
    class Launcher
    {
        private ProgressBar progressBar;
        private Button button;
        private Label label;

        private string BASE_URL = "http://www.mapleorigin.net/downloads/";
        private string checksumUrl;
        private string patchPath;

        private double currentProgress;
        private List<string> oldFiles;

        public Launcher(ProgressBar progressBar, Button button, Label label)
        {
            this.progressBar = progressBar;
            this.currentProgress = 0.0;
            this.button = button;
            this.label = label;
            this.checksumUrl = BASE_URL + "checksum.txt";
            this.patchPath = BASE_URL + "latest/";
            this.oldFiles = new List<string>();
        }

        public async void CheckForUpdates()
        {
            await Task.Run(() => download(checksumUrl, "temp\\", "checksum.txt", true)); // download checksum
        }

        public void PlayGame()
        {
            Process p = new Process();
            p.StartInfo.FileName = "MapleOrigin.exe";
            p.StartInfo.Arguments = "pnano.ddns.net 8484";
            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler(process_Exited);
            p.Start();
        }

        public void UpdateGame()
        {
            try
            {
                foreach (string file in oldFiles)
                {
                    string zipname = file.Split('.')[0] + ".zip";
                    download(patchPath + zipname, "temp\\", zipname);
                }
                updateButton(button, "Play Game", true);
            }
            catch (Exception e) // some file wasn't updated so we don't set it to play
            {
                show(e.Message + " Please close MapleOrigin and try again!");
                updateButton(button, "Update Game", true);
            }
        }

        private Task download(string url, string path, string filename, bool async = true)
        {
            using (var webClient = new WebClient())
            {
                System.IO.Directory.CreateDirectory(path); // create folder if not exists
                webClient.QueryString.Add("url", url);
                webClient.QueryString.Add("path", path);
                webClient.QueryString.Add("filename", filename);
                webClient.QueryString.Add("startMillis", "" + DateTime.Now.Millisecond);

                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(webClient_DownloadProgressChanged);

                Console.WriteLine("url: " + url + ", path: " + path + ", filename: " + filename);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(webClient_DownloadFileCompleted);
                return webClient.DownloadFileTaskAsync(new Uri(url), path + filename);
            }
        }

        private void processChecksums(string checksumFile)
        {
            string line;
            StreamReader file = new StreamReader(checksumFile);
            while ((line = file.ReadLine()) != null)
            {
                updateProgress(progressBar, currentProgress + (80 / 20));
                string[] split = line.Split(',');
                string filename = split[0];
                string remoteChecksum = split[1];
                string localChecksum = calculateChecksum(filename);
                if (!remoteChecksum.Equals(localChecksum))
                {
                    Console.WriteLine("Adding to queue: " + filename);
                    oldFiles.Add(filename);
                }
            }
            if (oldFiles.Count == 0) // player is up to date
            {
                updateButton(button, "Play Game", true);
                oldFiles = null;
            }
            else // not up to date
            {
                updateButton(button, "Update Game", true);
            }
            updateProgress(progressBar, 100);
            file.Close();
        }

        private string calculateChecksum(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }

        }

        private void processZip(string zipPath, string filename)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath + filename))
            {
                foreach (ZipArchiveEntry entry in archive.Entries) // only 1 file in the zip though
                {
                    Console.WriteLine("Extracting " + zipPath + filename + " to " + entry.FullName);
                    entry.ExtractToFile(entry.FullName, true);
                }
            }
            File.Delete(zipPath + filename);
        }

        private void webClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            WebClient client = ((WebClient)sender);
            if (e.Error == null)
            {
                updateProgress(progressBar, 0);
                int timeTaken = DateTime.Now.Millisecond - Int32.Parse(client.QueryString["startMillis"]);
                if (client.QueryString["url"].Equals(checksumUrl))
                {
                    processChecksums("temp\\checksum.txt"); // add files needed to be updated to list
                }
                else
                {
                    processZip(client.QueryString["path"], client.QueryString["filename"]);
                }
            }
            else
            {
                Console.WriteLine("error downloading: " + client.QueryString["url"]);
                show(e.Error.Message);
            }
            client.Dispose();
        }

        private void webClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            updateProgress(progressBar, e.ProgressPercentage);
        }

        private void process_Exited(object sender, EventArgs e)
        {
            updateButton(button, null, true);
        }

        private void show(string message)
        {
            MessageBoxEx.Show(Application.Current.MainWindow, message);
        }

        private void updateButton(Button button, String text, bool? isEnabled = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (text != null)
                    button.Content = text;
                if (isEnabled != null)
                    button.IsEnabled = (bool)isEnabled;
            });
        }

        private void updateProgress(ProgressBar bar, double val)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                bar.Value = val;
                currentProgress = val;
            });
        }

        private void updateLabel(Label label, String text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                label.Content = text;
            });
        }

    }
}
