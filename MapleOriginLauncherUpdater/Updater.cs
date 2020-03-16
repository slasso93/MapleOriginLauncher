using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace MapleOriginLauncherUpdater
{
    class Updater
    {
        public static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                string newLauncher = "temp\\MapleOriginLauncher.exe";
                string oldLauncher = args[0];

                downloadLauncher(newLauncher);

                Task.WaitAll(waitForAllLaunchersClosed(args[0].Split('.')[0]).ToArray()); // pass launcher exe name in case people rename the launcher

                // launchers all closed, we can replace the existing one now
                replaceLauncher(oldLauncher, newLauncher);

                // launch launcher again
                relaunch();
            }
        }

        private static void relaunch()
        {
            try
            {
                Process p = new Process();
                p.StartInfo.FileName = "MapleOriginLauncher.exe";
                p.Start();
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void downloadLauncher(string tempExe)
        {
            using (var webClient = new WebClient())
            {
                Console.WriteLine("Downloading new launcher");
                try
                {
                    webClient.DownloadFile(new Uri("https://drive.google.com/uc?export=download&id=1nRgB8A55QP331aR0VnuPeHcmz3E-BvfL"), tempExe);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static List<Task> waitForAllLaunchersClosed(string name)
        {
            List<Task> tasks = new List<Task>();

            Process[] pname = Process.GetProcessesByName(name); // get all launcher instances\
            if (pname.Length > 0)
            {
                Console.WriteLine("Found {0} open processes with name: {1}", pname.Length, name);
                Console.WriteLine("Waiting for processes to close.");
                foreach (var proc in pname)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        proc.WaitForExit();
                    }));
                }
            }
            return tasks;
        }

        private static void replaceLauncher(string oldLauncher, string newLauncher)
        {
            try
            {
                if (!File.Exists(newLauncher))
                {
                    Console.WriteLine("Nothing to replace.");
                }
                else
                {
                    if (File.Exists(oldLauncher))
                    {
                        Console.WriteLine("Deleting old " + oldLauncher);
                        File.Delete(oldLauncher);
                    }

                    Console.WriteLine("Moving " + newLauncher + " to MapleOrigin directory");
                    File.Move(newLauncher, "MapleOriginLauncher.exe"); // relative path is already maple origin folder due to forked process from Launcher which resides there
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if (File.Exists(newLauncher))
                {
                    Console.WriteLine("Attemping to cleanup temp file");
                    File.Delete(newLauncher);
                }
            }

        }

    }

}
