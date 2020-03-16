using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MapleOriginLauncher
{
    class Launcher
    {
        private int totalFiles = 18; // for checksum progress
        private ProgressBar progressBar;
        private Button button;
        private Label label;

        private string BASE_URL = "http://www.mapleorigin.net/downloads/";
        private string checksumUrl;
        private string versionUrl;
        private string updaterUrl;

        private bool launcherNeedsUpdate;
        private bool success;
        private double currentProgress;
        private Dictionary<string, int> filesToPatch; // filename, percentage downloaded
        private Dictionary<string, string> patchPaths; // filename, google drive link

        public Launcher(ProgressBar progressBar, Button button, Label label)
        {
            this.launcherNeedsUpdate = false;
            this.progressBar = progressBar;
            this.success = true;
            this.currentProgress = 0.0;
            this.button = button;
            this.label = label;
            this.checksumUrl = BASE_URL + "checksum.txt";
            this.versionUrl = BASE_URL + "version.txt";
            this.updaterUrl = BASE_URL + "MapleOriginLauncherUpdater.exe";
            this.filesToPatch = new Dictionary<string, int>();
            this.patchPaths = new Dictionary<string, string>();
        }

        public bool LauncherNeedsUpdate()
        {
            return this.launcherNeedsUpdate;
        }

        public async void CheckForUpdates()
        {
            await Task.Run(() => download(checksumUrl, "temp\\", "checksum.txt", true)); // download checksum
        }

        public async void RunLauncherUpdater()
        {
            await Task.Run(() =>
            {
                try
                {
                    download(updaterUrl, "temp\\", "MapleOriginLauncherUpdater.exe", false);
                    Process[] pname = Process.GetProcessesByName("MapleOriginLauncherUpdater"); // If another updater is already in progress, dont do anything
                    if (pname.Length == 0)
                    {
                        Process p = new Process();
                        p.StartInfo.FileName = "temp\\MapleOriginLauncherUpdater.exe";
                        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.Arguments = System.AppDomain.CurrentDomain.FriendlyName;
                        p.Start();
                        updateLabel(label, "Running.");
                    }

                }
                catch (Exception e)
                {
                    show(e.Message);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            });
        }

        public void PlayGame()
        {
            try
            {
                updateLabel(label, "MapleOrigin is starting.");
                Process p = new Process();
                p.StartInfo.FileName = "MapleOrigin.exe";
                p.StartInfo.Arguments = "pnano.ddns.net 8484";
                p.EnableRaisingEvents = true;
                p.Exited += new EventHandler(process_Exited);
                p.Start();
                updateLabel(label, "Running.");
            }
            catch (Exception e)
            {
                show(e.Message);
                updateLabel(label, "Ready to play.");
                updateButton(button, "Play Game", true);
            }
        }

        public async void UpdateGame()
        {
            success = true;
            updateProgress(progressBar, 0);
            try
            {
                List<Task> downloads = new List<Task>();

                foreach (string zipfile in filesToPatch.Keys)
                {
                    if (!File.Exists("temp\\" + zipfile))
                        downloads.Add(download(patchPaths[zipfile], "temp\\", zipfile));
                }

                await Task.WhenAll(downloads);
                int i = 1;
                currentProgress = 0;
                foreach (string zipfile in filesToPatch.Keys)
                {
                    if (success)
                    {
                        await Task.Run(() => processZip("temp\\", zipfile, i));
                        updateProgress(progressBar, currentProgress + 100 * ((double)(i++)) / filesToPatch.Count);
                    }
                }

                if (success)
                {
                    updateButton(button, "Play Game", true);
                }
                else
                {
                    updateProgress(progressBar, 100);
                }
            }
            catch (Exception e) // some file wasn't updated so we don't set it to play
            {
                show(e.Message + " Please close MapleOrigin and try again!");
                updateButton(button, "Update Game", true);
                updateProgress(progressBar, 100);
                success = false;
            }
        }

        private Task download(string url, string path, string filename, bool async = true)
        {
            using (var webClient = new WebClient())
            {
                Directory.CreateDirectory(path); // create folder if not exists
                webClient.QueryString.Add("url", url);
                webClient.QueryString.Add("path", path);
                webClient.QueryString.Add("filename", filename);
                webClient.QueryString.Add("startMillis", "" + DateTime.Now.Millisecond);

                Console.WriteLine("url: " + url + ", path: " + path + ", filename: " + filename);
                if (async)
                {
                    webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(webClient_DownloadProgressChanged);

                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(webClient_DownloadFileCompleted);
                    return webClient.DownloadFileTaskAsync(new Uri(url), path + filename);
                }
                else
                {
                    webClient.DownloadFile(url, path + filename);
                    return null;
                }
            }
        }

        private async void processChecksums(string checksumFile)
        {
            string line;
            using (StreamReader file = new StreamReader(checksumFile))
            {
                currentProgress = 0;

                // first line will be MapleOriginLauncher.exe
                string[] firstLine = file.ReadLine().Split(',');
                string launcherName = firstLine[0];
                string remoteLauncherChecksum = firstLine[1];
                string localLauncherChecksum = calculateChecksum(launcherName);

                List<string> filesInPatch = new List<string>();
                if (!remoteLauncherChecksum.Equals(localLauncherChecksum))
                {
                    Console.WriteLine("Launcher is outdated!");
                    launcherNeedsUpdate = true;
                }
                else
                {
                    bool patchUpdate = false;
                    await download(versionUrl, "temp\\", "version.txt", true);

                    using (StreamReader newVersion = new StreamReader("temp\\version.txt"))
                    {
                        string[] versionLine = newVersion.ReadLine().Split(',');
                        string version = versionLine[0];
                        string patchLink = versionLine[1];
                        if (!File.Exists("version.txt")) // if we dont have a versions.txt or version doesn't match, we will add all files from this patch to checksum exclusion (patch.zip will have these so we only need to checksum the others)
                        {
                            using (StreamWriter w = new StreamWriter("version.txt"))
                            {
                                w.WriteLine(version + "," + patchLink);
                                w.Write(newVersion.ReadToEnd());
                                newVersion.BaseStream.Position = 0;
                                newVersion.DiscardBufferedData();
                                newVersion.ReadLine();
                            }
                            patchUpdate = true;
                        }
                        else
                        {
                            using (StreamReader localVersion = new StreamReader("version.txt"))
                            {
                                patchUpdate = !localVersion.ReadLine().Split(',')[0].Equals(version);
                            }
                        }

                        if (patchUpdate)
                        {
                            filesToPatch.Add(version, 0);
                            patchPaths.Add(version, patchLink);
                            string patchLine;
                            while ((patchLine = newVersion.ReadLine()) != null)
                            {
                                filesInPatch.Add(patchLine);
                            }
                        }
                    }
                    File.Delete("temp\\version.txt");

                    while ((line = file.ReadLine()) != null)
                    {
                        string[] split = line.Split(',');
                        string filename = split[0];
                        string remoteChecksum = split[1];
                        string fileLink = split[2];
                        if (!filesInPatch.Contains(filename))
                        {
                            string localChecksum = calculateChecksum(filename);
                            if (!remoteChecksum.Equals(localChecksum))
                            {
                                Console.WriteLine("Adding to queue: " + filename);
                                filesToPatch.Add(filename.Split('.')[0] + ".zip", 0);
                                patchPaths.Add(filename.Split('.')[0] + ".zip", fileLink);
                            }
                        }
                        currentProgress += (100.0 / totalFiles);
                        updateProgress(progressBar, currentProgress);
                    }
                }
                if (filesToPatch.Count == 0) // player is up to date
                {
                    updateButton(button, "Play Game", true);
                    filesToPatch = null;
                }
                else // not up to date
                {
                    updateButton(button, "Update Game", true);
                }
            }
            File.Delete("temp\\checksum.txt");
        }

        private string calculateChecksum(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "";
            }

        }

        private void processZip(string zipPath, string filename, int count)
        {
            var tcs = new TaskCompletionSource<int>();
            if (success) // only keep extract zips if all of previous worked
            {
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(zipPath + filename))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries) // only 1 file in the zip though
                        {
                            updateLabel(label, "Extracting: " + entry.FullName + " (" + count + "/" + filesToPatch.Count + ")");
                            Console.WriteLine("Extracting " + entry.FullName + " from " + zipPath + filename);
                            entry.ExtractToFile(entry.FullName, true);
                        }
                    }
                    File.Delete(zipPath + filename);
                }
                catch (Exception e)
                {
                    show(e.Message);
                    updateButton(button, "Update Game", true);
                    updateLabel(label, "Updates pending!");
                    success = false;
                }
            }
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
            }
            else
            {
                Console.WriteLine("error downloading: " + client.QueryString["url"]);
                show(e.Error.Message);
                File.Delete(client.QueryString["path"] + client.QueryString["filename"]);
                success = false;
            }
            client.Dispose();
        }

        private void webClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            WebClient client = ((WebClient)sender);
            int newPercent = e.ProgressPercentage;
            if (filesToPatch.Count != 0)
            {
                string filename = client.QueryString["filename"];
                updateLabel(label, "Downloading " + filename);

                filesToPatch[filename] = newPercent;
                newPercent = filesToPatch.Sum(k => k.Value) / filesToPatch.Count;
                Console.WriteLine("download%: " + newPercent);
            }

            updateProgress(progressBar, newPercent);
        }

        private void process_Exited(object sender, EventArgs e)
        {
            updateLabel(label, "Ready to play.");
            updateButton(button, null, true);
        }

        private void show(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBoxEx.Show(Application.Current.MainWindow, message);
            });
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
