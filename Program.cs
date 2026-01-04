using System.Xml.Linq;
using s3pi.Interfaces;
using s3pi.Package;

namespace S3BuildTool
{
    static class Sims3Constants
    {
        public const uint Type_S3SA = 0x073FAA07;
        public const ulong Fnv64Prime = 0x100000001b3;
        public const ulong Fnv64Offset = 0xcbf29ce484222325;
    }

    class Program
    {
        static int Main(string[] args)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                var paramsMap = ParseArguments(args);
                
                if (!paramsMap.ContainsKey("modname") || !paramsMap.ContainsKey("dllpath"))
                {
                    Log("Usage: ts3buildtool.exe -modName=\"Name\" -dllPath=\"$(TargetPath)\" [-skip=\"folder1\"]", ConsoleColor.Red);
                    return 1;
                }

                string modName = paramsMap["modname"];
                string dllPath = paramsMap["dllpath"].Trim('\"');
                string defaultSubPath = paramsMap.TryGetValue("defaultpath", out var value) ? value.Trim('\"') : "Packages";
                
                List<string> skipList = paramsMap.TryGetValue("skip", out var value1) 
                    ? value1.Split(',').Select(s => s.Trim()).ToList() 
                    : new List<string>();

                Log($"Building package \"{modName}\"...", ConsoleColor.White);

                if (!File.Exists(dllPath))
                    throw new FileNotFoundException($"Source DLL not found at: {dllPath}");

                string toolDir = AppDomain.CurrentDomain.BaseDirectory;
                string? solutionDir = Directory.GetParent(toolDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
                string modsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts", "The Sims 3", "Mods");

                string packagePath = FindOrDefinePackagePath(modsDir, modName, skipList, defaultSubPath);
                
                bool placeholderFound = solutionDir != null && BuildBasePackage(solutionDir, packagePath);

                if (placeholderFound)
                {
                    InjectS3SA(packagePath, dllPath);
                }
                else
                {
                    Log("Warning: No S3SA (0x073FAA07) resource entry found in nameMap.xml. DLL injection skipped.", ConsoleColor.Yellow);
                }

                Log("[SUCCESS] Mod updated successfully.", ConsoleColor.Green);
                return 0;
            }
            catch (Exception ex)
            {
                Log($"[FATAL ERROR] {ex.Message}", ConsoleColor.Red);
                return 1;
            }
            finally { Console.ForegroundColor = originalColor; }
        }

        static bool BuildBasePackage(string solutionDir, string packagePath)
        {
            Log("Reading resources...", ConsoleColor.Cyan);
            string? resDir = Directory.GetDirectories(solutionDir, "resources", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(resDir)) throw new Exception("Resources folder not found in solution.");

            if (File.Exists(packagePath)) File.Delete(packagePath);

            IPackage pkg = Package.NewPackage(1);
            XDocument xml = XDocument.Load(Path.Combine(resDir, "nameMap.xml"));
            bool hasS3SA = false;

            foreach (var res in xml.Descendants("resource"))
            {
                string resName = res.Attribute("name")?.Value ?? "Unknown";
                uint rType = Convert.ToUInt32(res.Attribute("type")?.Value ?? "0", 16);
                ulong hash = HashFNV64(resName);
                IResourceKey key = new TGIBlock(1, null, rType, 0, hash);

                Log($"Adding {resName} (0x{hash:X16}) to \"{Path.GetFileName(packagePath)}\"", ConsoleColor.DarkCyan);

                if (rType == Sims3Constants.Type_S3SA)
                {
                    hasS3SA = true;
                    pkg.AddResource(key, new MemoryStream([]), false);
                }
                else
                {
                    string? file = Directory.GetFiles(resDir, "*.*", SearchOption.AllDirectories)
                        .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(resName, StringComparison.OrdinalIgnoreCase) 
                                          || Path.GetFileName(f).Equals(resName, StringComparison.OrdinalIgnoreCase));

                    if (file != null && !Path.GetFileName(file).Equals("nameMap.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        pkg.AddResource(key, new MemoryStream(File.ReadAllBytes(file)), false);
                    }
                    else
                    {
                        Log($"Warning: Resource file \"{resName}\" not found on disk!", ConsoleColor.Yellow);
                    }
                }
            }

            pkg.SaveAs(packagePath);
            Package.ClosePackage(1, pkg);
            return hasS3SA;
        }

        static void InjectS3SA(string pkgPath, string dllPath)
        {
            Log($"Injecting DLL into S3SA resource...", ConsoleColor.Cyan);
            IPackage pkg = Package.OpenPackage(1, pkgPath, true);
            var entry = pkg.FindAll(e => e.ResourceType == Sims3Constants.Type_S3SA).FirstOrDefault();
            
            if (entry != null)
            {
                var script = new ScriptResource.ScriptResource(1, null);
                using (FileStream fs = File.OpenRead(dllPath))
                {
                    Log($"Loading assembly: {Path.GetFileName(dllPath)}", ConsoleColor.DarkGray);
                    script.Assembly = new BinaryReader(fs);
                    pkg.ReplaceResource(entry, script);
                }
                pkg.SavePackage();
            }
            Package.ClosePackage(1, pkg);
        }

        static string FindOrDefinePackagePath(string modsDir, string name, List<string> skip, string defaultSubPath)
        {
            string pattern = name + ".package";
            var files = Directory.Exists(modsDir) ? Directory.GetFiles(modsDir, pattern, SearchOption.AllDirectories) : [];
            
            var existing = files.FirstOrDefault(p => !p.Split(Path.DirectorySeparatorChar).Any(part => skip.Contains(part, StringComparer.OrdinalIgnoreCase)));

            if (existing != null)
            {
                Log($"Found existing package at: {existing}", ConsoleColor.DarkGray);
                return existing;
            }

            Log($"Package not found in Mods. Creating new at: {defaultSubPath}", ConsoleColor.DarkGray);
            string finalDir = Path.Combine(modsDir, defaultSubPath);
            if (!Directory.Exists(finalDir)) Directory.CreateDirectory(finalDir);
            return Path.Combine(finalDir, name.Replace("*", "") + ".package");
        }

        static Dictionary<string, string> ParseArguments(string[] args)
        {
            var map = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-") && arg.Contains("="))
                {
                    var parts = arg.Substring(1).Split(['='], 2);
                    map[parts[0].ToLower()] = parts[1];
                }
            }
            return map;
        }

        public static ulong HashFNV64(string input)
        {
            input = input.ToLowerInvariant();
            ulong hash = Sims3Constants.Fnv64Offset;
            foreach (char c in input) { hash *= Sims3Constants.Fnv64Prime; hash ^= (byte)c; }
            return hash;
        }

        static void Log(string m, ConsoleColor c) 
        { 
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("[ts3buildtool] ");
            Console.ForegroundColor = c; 
            Console.WriteLine(m); 
            Console.ResetColor(); 
        }
    }
}