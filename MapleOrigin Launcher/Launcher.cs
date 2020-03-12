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
using System.Windows;
using System.Windows.Controls;

namespace MapleOrigin_Launcher
{
    class Launcher
    {
        public ProgressBar progressBar;
        public Button playButton;
        public Button updateButton;

        private string BASE_URL = "http://www.mapleorigin.net/downloads/";
        private string checksumUrl;
        private string patchPath;

        public Launcher(ProgressBar progressBar, Button playButton, Button updateButton)
        {
            this.progressBar = progressBar;
            this.playButton = playButton;
            this.updateButton = updateButton;
            this.checksumUrl = BASE_URL + "checksum.txt";
            this.patchPath = BASE_URL + "latest/";
        }

        public void PlayGame()
        {
            this.updateButton.IsEnabled = false;
            this.playButton.IsEnabled = false;
            Process.Start("MapleOrigin.exe");
        }

        public void UpdateGame()
        {
            this.updateButton.IsEnabled = false;
            this.playButton.IsEnabled = false;

            System.IO.Directory.CreateDirectory("temp");
            download(checksumUrl, "temp/checksum.txt", "checksum.txt"); // download checksum async. Response handled in webClient_DownloadFileCompleted
        }

        private void download(string url, string path, string filename)
        {
            using (var webClient = new WebClient())
            {
                webClient.QueryString.Add("url", url);
                webClient.QueryString.Add("path", path);
                webClient.QueryString.Add("filename", filename);
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(webClient_DownloadFileCompleted);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(webClient_DownloadProgressChanged);
                webClient.DownloadFileAsync(new Uri(url), path);
            }
        }

        private void processChecksums()
        {
            string line;
            StreamReader file = new StreamReader("temp\\checksum.txt");
            while ((line = file.ReadLine()) != null)
            {
                string[] split = line.Split(',');
                string filename = split[0];
                string remoteChecksum = split[1];
                string localChecksum = calculateChecksum(filename);
                if (!remoteChecksum.Equals(localChecksum))
                {
                    string zipname = filename.Split('.')[0] + ".zip";
                    Console.WriteLine("Downloading " + zipname + " to temp/");
                    download(patchPath + zipname, "temp\\" + zipname, filename);
                }
            }

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
            Console.WriteLine("Extracting " + zipPath + " to " + filename);

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                archive.GetEntry(filename).ExtractToFile(filename, true);
            }
            File.Delete(zipPath);
        }

        private void webClient_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            WebClient client = ((WebClient)sender);
            if (e.Error == null)
            {
                progressBar.Value = 0;

                if (client.QueryString["url"].Equals(checksumUrl))
                {
                    processChecksums();
                }
                else // it's an updated file in zip
                {
                    processZip(client.QueryString["path"], client.QueryString["filename"]);
                   
                }
            }
            else
            {
                MessageBox.Show(e.Error.Message);
            }
            client.Dispose();
        }

        private void webClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }

    }
}
