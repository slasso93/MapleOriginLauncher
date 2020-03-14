using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MapleOriginPackager
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Please enter the source and destination paths. EX: mapleoriginpacker.exe path/to/latest_unpacked/ path/to/downloads/latest path/to/downloads/checksum.txt");
            }
            else
            {
                string sourceFolder = args[0];
                string destFolder = args[1];
                string checksumPath = args[2];
                Console.WriteLine("Packing files in " + sourceFolder);
                List<string> updatedFiles = overwriteChecksums(sourceFolder, checksumPath); // replace the checksums for this patch

                if (updatedFiles.Count > 0)
                    Console.WriteLine("Outdated: " + String.Join(", ", updatedFiles.Select(x => Path.GetFileName(x))));
                else
                    Console.WriteLine("Nothing to zip, all files up to date.");
                addToDest(updatedFiles, destFolder);
            }
            Console.WriteLine("Done, press any key to continue...");
            Console.ReadLine();
        }

        private static void addToDest(List<string> files, string destFolder)
        {
            foreach (var file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Contains("MapleOriginLauncher"))
                {
                    if (File.Exists(destFolder + filename))
                    {
                        File.Delete(destFolder + filename);
                    }
                    File.Copy(file, destFolder + filename);
                }
                else
                {
                    using (FileStream stream = new FileStream(destFolder + filename.Split('.')[0] + ".zip", FileMode.Create))
                    {
                        using (var arch = new ZipArchive(stream, ZipArchiveMode.Create))
                        {
                            Console.WriteLine("Zipping " + filename);
                            arch.CreateEntryFromFile(file, filename);
                        }
                    }
                }
            }
        }

        private static List<string> overwriteChecksums(string folder, string checksumTxt)
        {
            List<string> updatedFiles = new List<string>();
            try
            {
                string line;
                using (StreamReader checksumFile = File.Exists(checksumTxt) ? new StreamReader(checksumTxt) : null)
                {
                    Console.WriteLine("Calculating checksums");
                    List<string> newLines = new List<string>();
                    foreach (var file in Directory.GetFiles(folder).OrderByDescending(name => name).Reverse().Where(s => !s.Contains("MapleOriginLauncherUpdater")))
                    {
                        string newChecksum = calculateChecksum(file);
                        if (checksumFile != null)
                        {
                            line = checksumFile.ReadLine();
                            if (line != null && line.Split(',')[1].Equals(newChecksum)) // this mean file is up to date
                            { 
                                continue;
                            }
                        }
                        if (Path.GetFileName(file).Equals("MapleOriginLauncher.exe"))
                        {
                            updatedFiles.Insert(0, file);
                            newLines.Insert(0, Path.GetFileName(file) + "," + newChecksum);
                        }
                        else
                        {
                            updatedFiles.Add(file);
                            newLines.Add(Path.GetFileName(file) + "," + newChecksum);
                        }
                    }
                    File.WriteAllLines(".\\checksum.txt", newLines.ToArray());
                }
                if (File.Exists(checksumTxt))
                    File.Replace(".\\checksum.txt", checksumTxt, null);
                else
                    File.Move(".\\checksum.txt", checksumTxt);
            } 
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return updatedFiles; // we will only zip these updated files
        }

        private static string calculateChecksum(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        string checksum = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                        Console.WriteLine(Path.GetFileName(filename) + ": " + checksum);
                        return checksum;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return "";
            }
        }
    }
}
