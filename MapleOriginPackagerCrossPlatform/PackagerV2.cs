using System;
using System.IO;
using MapleLib.WzLib;
using MapleLib.WzLib.Serialization;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace MapleOriginPackagerCrossPlatform
{

    class PackagerV2
    {

        static WzMapleVersion version = WzMapleVersion.GMS;
        static IWzFileSerializer serializer = new WzImgSerializer();

        static int Main(string[] args)
        {
            var setupPath = args[0];
            var patchDir = args[1];
            Console.WriteLine($"Attempting to create new patch for {setupPath}.");
            Console.WriteLine($"Patch base directory: {patchDir}");

            makePatch(setupPath, patchDir);

            return 1;
        }

        static void dumpWz(string wzPath, string dest)
        {
            WzFile wz = new WzFile(wzPath, version);
            Console.WriteLine($"Dumping {wz.Name}");
            wz.ParseWzFile();
            serializer.SerializeFile(wz, Path.Combine(dest, wz.Name));
            wz.Dispose();
        }

        public static void makePatch(string setupPath, string patchDir)
        {
            // Get all files in the setupPath directory
            var files = Directory.EnumerateFiles(setupPath);

            // Create the patch directory if it doesn't exist
            Directory.CreateDirectory(patchDir);

            var patches = Directory.EnumerateDirectories(patchDir); // existing patches
            var patchChecksums = UpdateChecksumFile(Path.Combine(patchDir, "checksum.txt"), files); // update checksum and get the delta back

            // If there are no existing patches or we have some changes
            if (!patches.Any() || patchChecksums.Count > 0)
            {
                // Create a new patch directory using the current time as the name
                var patchPath = Path.Combine(patchDir, DateTime.Now.ToString("yyyyMMddHHmmss"));
                Directory.CreateDirectory(patchPath);

                Console.WriteLine($"Creating patch {patchPath}");

                // Copy all changed files from setupPath to the patch directory
                CopyFilesToPatchFolder(files, patchPath, patchChecksums);

                // create patch.txt
                CreatePatchFile(patchPath, patchChecksums);
            }
            else
            {
                Console.WriteLine("No changes found. No patch necessary");
            }
        }

        private static void CopyFilesToPatchFolder(IEnumerable<string> files, string patchPath, Dictionary<string, string> patchChecksums)
        {
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                if (patchChecksums.ContainsKey(filename))
                {
                    Console.WriteLine($"CopyFilesToPatchFolder: {filename} changed. Adding to patch");

                    if (filename.EndsWith(".wz"))
                    {
                        dumpWz(file, patchPath);
                    }
                    else
                    {
                        File.Copy(file, Path.Combine(patchPath, filename), true);
                    }
                }
            }
        }

        public static void CreatePatchFile(string patchPath, Dictionary<string, string> patchChecksums)
        {
            Console.WriteLine("CreatePatchFile: Creating patch.txt");

            // Create the patch file path
            var patchFile = Path.Combine(patchPath, "patch.txt");

            // Write the patch header to the patch file
            File.WriteAllLines(patchFile, new[] { $"location,{patchPath}" });

            // Write the patch checksums to the patch file
            File.AppendAllLines(patchFile, patchChecksums.Select(entry => $"{entry.Key},{entry.Value}"));
        }

        public static Dictionary<string, string> GetChecksums(IEnumerable<string> files)
        {
            var checksums = new Dictionary<string, string>();

            Console.WriteLine($"Getting checksums for all files: {files}");
            foreach (string file in files)
            {
                using (var stream = new FileStream(file, FileMode.Open))
                using (var hashAlgorithm = MD5.Create())
                {
                    var hash = hashAlgorithm.ComputeHash(stream);
                    checksums[file] = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
                }
            }

            return checksums;
        }

        public static Dictionary<string, string> UpdateChecksumFile(string checksumFile, IEnumerable<string> files)
        {
            var patchChecksums = new Dictionary<string, string>();

            // Get the new checksums for the given files
            var newChecksums = GetChecksums(files);

            // Read the existing checksums from the checksum file
            var existingChecksums = File.Exists(checksumFile)
                ? File.ReadAllLines(checksumFile)
                    .Select(line => line.Split(','))
                    .ToDictionary(parts => Path.GetFileName(parts[0]), parts => parts[1])
                : new Dictionary<string, string>();

            // Update the existing checksums with the new ones
            foreach (var entry in newChecksums)
            {
                var filename = Path.GetFileName(entry.Key);
                var hasKey = existingChecksums.ContainsKey(filename);

                if (!hasKey || (hasKey && existingChecksums[filename] != entry.Value))
                {
                    patchChecksums[filename] = entry.Value;
                }
            }

            // Write the updated checksums to the checksum file
            File.WriteAllLines(checksumFile, newChecksums.Select(entry => $"{Path.GetFileName(entry.Key)},{entry.Value}"));

            return patchChecksums;
        }

    }

}
