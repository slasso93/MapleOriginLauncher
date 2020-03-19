using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace MapleOriginPackagerCrossPlatform
{
    class Program
    {

        static string[] Scopes = new string[] {
            DriveService.Scope.Drive,
            DriveService.Scope.DriveFile,
            DriveService.Scope.DriveMetadata,
            DriveService.Scope.DriveAppdata,
            DriveService.Scope.DriveScripts
        };
        static string HOST = "34.192.141.86";
        static string BASE_URL = "http://" + HOST + "/downloads/";
        static string ApplicationName = "MapleOriginPackager";
        static string patchDriveFolder = "1Jc3y4a3iD9b3BAldgIwue5_Rc1NsyrcH"; // patch folder containing latest folder and all patch.zip files
        static string latestDriveFolder = "1bG3B7Y-H37km1phbyfOmH0rnoSijeWK4"; // folder for all latest separately zipped files
        static string separate = "=======================================================================================================================";

        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Command format:");
                Console.WriteLine("'MapleOriginPacker.exe <path_to_patchfolder> <path_to_downloads> <true|false>' (flag is whether or not to zip and upload as patch, individual diffs still upload)");
            }
            else
            {
                string sourceFolder = args[0];
                string downloadsDir = args[1];
                bool shouldCreatePatch;
                Boolean.TryParse(args[2], out shouldCreatePatch);

                Console.WriteLine("Showing available patch folders.");
                DirectoryInfo dirInfo = new DirectoryInfo(sourceFolder);

                List<string> patchFolders = new List<string>();
                foreach (var dir in dirInfo.EnumerateDirectories().OrderByDescending(d => d.CreationTime))
                {
                    Console.WriteLine((patchFolders.Count + 1) + ": {0}", dir.Name);
                    patchFolders.Add(dir.FullName);
                }

                Console.Write("Please enter a #: ");
                int sel;
                string input = Console.ReadLine();
                if (input.Equals("q") || input.Equals("quit") || input.Equals("exit"))
                    return 0;
                bool parsed = Int32.TryParse(input, out sel);
                if (sel < 1 || sel > patchFolders.Count)
                    sel = 1;

                string patchFolder = patchFolders[sel - 1] + "/";
                Console.WriteLine(separate);
                Console.WriteLine("Packing files in " + patchFolder);
                Dictionary<string, string> checksumMap = new Dictionary<string, string>();
                List<string> updatedFiles = overwriteChecksums(checksumMap, patchFolder, downloadsDir + "/checksum.txt"); // replace the checksums for this patch

                if (updatedFiles.Count > 0)
                {
                    Console.WriteLine(separate);
                    Console.WriteLine("Outdated: " + String.Join(", ", updatedFiles.Select(x => Path.GetFileName(x))));
                    updateOutdated(checksumMap, updatedFiles, downloadsDir);
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("All files up to date");
                }
                writeChecksumFile(checksumMap, downloadsDir + "/checksum.txt");

                if (shouldCreatePatch)
                {
                    if (updatedFiles.Count == 0)
                    {
                        Console.WriteLine("Nothing to patch");
                    }
                    else
                    {
                        string patchName = "MapleOrigin_patch_" + DateTime.Now.ToString("MM_dd_yyyy");
                        string patchZip = downloadsDir + "/" + patchName + ".zip";
                        List<string> versionLines = createPatch(patchFolder, patchZip, patchName);
                        if (versionLines.Count > 0)
                        {
                            //string link = uploadToDrive(patchZip, patchDriveFolder, null);
                            versionLines.Insert(0, String.Format("{0},{1}", Path.GetFileName(patchZip), BASE_URL + Path.GetFileName(patchZip)));
                            File.WriteAllLines(downloadsDir + "/version.txt", versionLines);
                        }
                    }
                }
            }
            Console.WriteLine(separate);
            Console.Write("Done, press any key to continue...");
            Console.ReadLine();
            return 0;
        }

        private static string uploadToDrive(string fileToUpload, string parentDriveFolder, string fileId)
        {
            var service = initGoogleAuth();
            Google.Apis.Drive.v3.Data.File fileMetaData = new Google.Apis.Drive.v3.Data.File();
            fileMetaData.Name = Path.GetFileName(fileToUpload);
            fileMetaData.Parents = new string[] { parentDriveFolder };

            Console.WriteLine("Uploading to Google Drive: " + fileMetaData.Name);
            FilesResource.CreateMediaUpload createRequest = null;
            FilesResource.UpdateMediaUpload updateRequest = null;
            using (var stream = new FileStream(fileToUpload, FileMode.Open))
            {
                if (fileId != null)
                {
                    updateRequest = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), fileId, stream, null);
                    updateRequest.Fields = "id";
                    updateRequest.Upload();
                }
                else
                {
                    createRequest = service.Files.Create(fileMetaData, stream, null);
                    createRequest.Fields = "id";
                    createRequest.Upload();
                }
            }
            fileId = createRequest != null ? fileId = createRequest.ResponseBody.Id : fileId;

            Google.Apis.Drive.v3.Data.Permission readPermission = new Google.Apis.Drive.v3.Data.Permission();
            readPermission.Role = "reader";
            readPermission.Type = "anyone";
            service.Permissions.Create(readPermission, fileId).Execute();

            string shareableUrl = "https://drive.google.com/uc?export=download&id=" + fileId;
            Console.WriteLine("Successfully uploaded " + fileMetaData.Name + ": " + shareableUrl);

            return shareableUrl;
        }

        private static DriveService initGoogleAuth()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;

            }
            return new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }

        private static List<string> createPatch(string patchFolder, string finalZip, string patchName)
        {
            List<string> versionLines = new List<string>();

            Console.WriteLine(separate);
            Console.WriteLine("Creating new patch: " + patchName);

            if (File.Exists(finalZip)) // if it exists are we're running the packager again wtih new files, delete the old zip first
            {
                File.Delete(finalZip);
            }

            using (var zip = ZipFile.Open(finalZip, ZipArchiveMode.Create))
            {
                foreach (var file in new DirectoryInfo(patchFolder).EnumerateFiles())
                {
                    if (!file.Name.Equals("MapleOriginLauncher.exe"))
                    {
                        versionLines.Add(file.Name);
                        zip.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
                    }
                }
            }

            return versionLines;
        }

        private static void updateOutdated(Dictionary<string, string> checksumMap, List<string> files, string destFolder)
        {
            Directory.CreateDirectory(destFolder + "/latest/");
            bool isZip = false;
            foreach (var file in files)
            {
                Console.WriteLine();
                string filename = Path.GetFileName(file);
                string dest = destFolder + "/latest/" + filename;
                if (filename.Contains("MapleOriginLauncher"))
                {
                    if (File.Exists(dest))
                    {
                        File.Delete(dest);
                    }
                    Console.WriteLine("Copying " + filename);
                    File.Copy(file, dest);
                }
                else
                {
                    isZip = true;
                    dest = dest.Split('.')[0] + ".zip";
                    using (FileStream stream = new FileStream(dest, FileMode.Create))
                    {
                        using (var arch = new ZipArchive(stream, ZipArchiveMode.Create))
                        {
                            Console.WriteLine("Zipping " + filename);
                            arch.CreateEntryFromFile(file, filename);
                        }
                    }
                }

                /*string fileId = null;
                string val = checksumMap[filename];
                if (val.LastIndexOf("id=") != -1)
                {
                    int idIdx = val.LastIndexOf("id=") + 3;
                    fileId = val.Substring(idIdx);
                }

                string link = uploadToDrive(dest, latestDriveFolder, fileId);*/
                //if (fileId == null) // append shareable link if this is the first upload
                if (checksumMap[filename].Split(',').Length == 1)
                    checksumMap[filename] += "," + BASE_URL + "latest/" + (isZip ? filename.Split('.')[0] + ".zip" : filename);
            }

        }

        private static void writeChecksumFile(Dictionary<string, string> checksumMap, string checksumTxt)
        {
            string launcherVal;
            if (checksumMap.TryGetValue("MapleOriginLauncher.exe", out launcherVal))
                checksumMap.Remove("MapleOriginLauncher.exe");

            List<string> newLines = checksumMap.Select(e => e.Key + "," + e.Value).OrderBy(line => line.Split(',').First()).ToList();
            if (launcherVal != null)
                newLines.Insert(0, "MapleOriginLauncher.exe" + "," + launcherVal);
            File.WriteAllLines(checksumTxt, newLines);
        }

        private static List<string> overwriteChecksums(Dictionary<String, String> checksumMap, string folder, string checksumTxt)
        {
            List<string> updatedFiles = new List<string>();
            try
            {
                using (StreamReader checksumFile = File.Exists(checksumTxt) ? new StreamReader(checksumTxt) : null)
                {
                    Console.WriteLine("Calculating checksums");

                    string launcherName = "MapleOriginLauncher.exe";
                    if (File.Exists(folder + launcherName))
                        checksumDiff(checksumMap, updatedFiles, checksumFile, folder + launcherName);
                    else if (checksumFile != null && checksumFile.Peek() != -1)
                    {
                        string launcherLine = checksumFile.ReadLine();
                        if (launcherLine.StartsWith(launcherName))
                        {
                            string[] line = launcherLine.Split(',');
                            string k = line[0];
                            string v = line[1];
                            if (line.Length == 3) // has url
                                v += ',' + line[2];
                            checksumMap.Add(k, v);
                        }
                        else
                        {
                            checksumFile.BaseStream.Position = 0;
                            checksumFile.DiscardBufferedData();
                        }
                    }

                    foreach (var file in Directory.GetFiles(folder).OrderBy(name => name).Where(s => !Path.GetFileName(s).Contains("MapleOriginLauncher")))
                    {
                        checksumDiff(checksumMap, updatedFiles, checksumFile, file);
                    }
                    if (checksumFile != null && checksumFile.Peek() != -1)
                    {
                        string remainingLines;
                        while ((remainingLines = checksumFile.ReadLine()) != null)
                        {
                            string[] line = remainingLines.Split(',');
                            string k = line[0];
                            string v = line[1];
                            if (line.Length == 3) // has url
                                v += ',' + line[2];
                            checksumMap.Add(k, v);
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return updatedFiles; // we will only zip these updated files
        }

        private static void checksumDiff(Dictionary<string, string> checksumMap, List<string> updatedFiles, StreamReader checksumFile, string file)
        {
            string sourceChecksum = calculateChecksum(file);

            if (checksumFile != null && checksumFile.Peek() != -1)
            {
                string line;
                while ((line = checksumFile.ReadLine()) != null)
                {
                    string[] lineSplit = line.Split(',');
                    string nameInFile = lineSplit[0];
                    string oldChecksum = lineSplit[1];
                    string url = lineSplit.Length == 3 ? lineSplit[2] : null; // has url

                    if (!Path.GetFileName(file).Equals(nameInFile)) // name in file could be Base.wz, but we are looking for Character.wz, for example
                    {
                        checksumMap.Add(nameInFile, oldChecksum + (url != null ? "," + url : "")); // keep the old line where it is
                    }
                    else if (!oldChecksum.Equals(sourceChecksum)) // we are replacing this line if the checksums dont match
                    {
                        checksumMap.Add(nameInFile, sourceChecksum + (url != null ? "," + url : ""));
                        updatedFiles.Add(file);
                        break;
                    }
                    else // we got a checksum match so we will just keep the row
                    {
                        checksumMap.Add(nameInFile, sourceChecksum + (url != null ? "," + url : ""));
                        break;
                    }

                }
            }

            if (!checksumMap.ContainsKey(Path.GetFileName(file))) // checksum is empty or missing or the file is new
            {
                checksumMap.Add(Path.GetFileName(file), sourceChecksum);
                updatedFiles.Add(file);
            }
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
