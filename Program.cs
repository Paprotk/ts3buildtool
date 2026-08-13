using System.Xml.Linq;
using s3pi.Interfaces;
using s3pi.Package;

namespace S3BuildTool
{
    // --- CONSTANTS AND MAPPINGS ---
    static class Sims3Constants
    {
        public const uint Type_NameMap = 0x0166038C;
        public const uint Type_S3SA = 0x073FAA07;
        public const uint Type_STBL = 0x220557DA;
        public const ulong Fnv64Prime = 0x100000001b3;
        public const ulong Fnv64Offset = 0xcbf29ce484222325;

        public static readonly Dictionary<string, byte> LocaleMap = new()
        {
            { "ENG_US", 0x00 }, { "CHS_CN", 0x01 }, { "CHT_CN", 0x02 }, { "CZE_CZ", 0x03 },
            { "DAN_DK", 0x04 }, { "DUT_NL", 0x05 }, { "FIN_FI", 0x06 }, { "FRE_FR", 0x07 },
            { "GER_DE", 0x08 }, { "GRE_GR", 0x09 }, { "HUN_HU", 0x0A }, { "ITA_IT", 0x0B },
            { "JPN_JP", 0x0C }, { "KOR_KR", 0x0D }, { "NOR_NO", 0x0E }, { "POL_PL", 0x0F },
            { "POR_PT", 0x10 }, { "POR_BR", 0x11 }, { "RUS_RU", 0x12 }, { "SPA_ES", 0x13 },
            { "SPA_MX", 0x14 }, { "SWE_SE", 0x15 }, { "THA_TH", 0x16 }
        };
    }

    interface IResourceProcessor
    {
        uint TargetType { get; }
        bool CanHandle(uint type, string name);

        void Process(IPackage pkg, string resName, uint resType, string resDir, string modName,
            Dictionary<ulong, string> nameMap, string dllPath);
    }

    // --- PROCESSOR IMPLEMENTATIONS ---

    class S3SAProcessor : IResourceProcessor
    {
        public uint TargetType => Sims3Constants.Type_S3SA;
        public bool CanHandle(uint type, string name) => type == TargetType;

        public void Process(IPackage pkg, string resName, uint resType, string resDir, string modName,
            Dictionary<ulong, string> nameMap, string dllPath)
        {
            ulong hash = Program.HashFNV64(resName);
            IResourceKey key = new TGIBlock(1, null, resType, 0, hash);

            if (File.Exists(dllPath))
            {
                var script = new ScriptResource.ScriptResource(1, null);
                using (FileStream fs = File.OpenRead(dllPath))
                {
                    script.Assembly = new BinaryReader(fs);
                    pkg.AddResource(key, script.Stream, false);
                }

                Program.Log($"[S3SA] Injected DLL: {resName} (0x{hash:X16})", ConsoleColor.Cyan);
            }
            else
            {
                Program.Log($"Warning: DLL not found at {dllPath}. Reserving empty S3SA.", ConsoleColor.Yellow);
                pkg.AddResource(key, new MemoryStream(), false);
                Console.Beep(600, 200);
            }

            if (!nameMap.ContainsKey(hash)) nameMap.Add(hash, resName);
        }
    }

    class STBLProcessor : IResourceProcessor
    {
        public uint TargetType => Sims3Constants.Type_STBL;
        public bool CanHandle(uint type, string name) => type == TargetType;

        public void Process(IPackage pkg, string resName, uint resType, string resDir, string modName,
            Dictionary<ulong, string> nameMap, string dllPath)
        {
            if (resName == "*.stbl")
            {
                Program.Log($"Processing STBL wildcard for mod: {modName}", ConsoleColor.DarkCyan);
                ulong modBaseHash = Program.HashFNV64(modName) & 0x00FFFFFFFFFFFFFF;
                Program.Log($"Mod base hash: 0x{modBaseHash:X16}", ConsoleColor.DarkCyan);

                var stblFiles = Directory.GetFiles(resDir, "*.stbl", SearchOption.AllDirectories);
                Program.Log($"Found {stblFiles.Length} .stbl files total", ConsoleColor.DarkCyan);

                if (stblFiles.Length == 0)
                {
                    Program.Log("Warning: No .stbl files found for wildcard search!", ConsoleColor.Yellow);
                }

                foreach (var file in stblFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string fullPath = Path.GetFullPath(file);
                    Program.Log($"Processing STBL file: {fileName} at {fullPath}", ConsoleColor.DarkCyan);

                    var locale = Sims3Constants.LocaleMap.FirstOrDefault(x => fileName.EndsWith(x.Key));

                    if (locale.Key != null)
                    {
                        ulong instanceId = ((ulong)locale.Value << 56) | modBaseHash;
                        pkg.AddResource(new TGIBlock(1, null, resType, 0, instanceId),
                            new MemoryStream(File.ReadAllBytes(file)), false);
                        if (!nameMap.ContainsKey(instanceId)) nameMap.Add(instanceId, fileName);
                        Program.Log($"[STBL] Injected {fileName} (locale: {locale.Key}, id: 0x{instanceId:X16})",
                            ConsoleColor.DarkCyan);
                    }
                    else
                    {
                        Program.Log($"Warning: STBL file '{fileName}' doesn't end with a valid locale code!",
                            ConsoleColor.Yellow);
                        Program.Log($"  Valid locales: {string.Join(", ", Sims3Constants.LocaleMap.Keys)}",
                            ConsoleColor.Yellow);
                    }
                }
            }
            else
            {
                ulong hash = Program.HashFNV64(resName);
                string? file = Program.FindFile(resDir, resName);
                if (file != null)
                {
                    pkg.AddResource(new TGIBlock(1, null, resType, 0, hash), new MemoryStream(File.ReadAllBytes(file)),
                        false);
                    if (!nameMap.ContainsKey(hash)) nameMap.Add(hash, resName);
                    Program.Log($"[STBL] Added {resName} (0x{hash:X16})", ConsoleColor.DarkCyan);
                }
                else Program.Log($"Warning: STBL file '{resName}' not found on disk!", ConsoleColor.Yellow);
            }
        }
    }

    class GenericProcessor : IResourceProcessor
    {
        public uint TargetType => 0;
        public bool CanHandle(uint type, string name) => true;

        public void Process(IPackage pkg, string resName, uint resType, string resDir, string modName,
            Dictionary<ulong, string> nameMap, string dllPath)
        {
            ulong hash = Program.HashFNV64(resName);
            string? file = Program.FindFile(resDir, resName);

            if (file != null)
            {
                pkg.AddResource(new TGIBlock(1, null, resType, 0, hash), new MemoryStream(File.ReadAllBytes(file)),
                    false);
                if (!nameMap.ContainsKey(hash)) nameMap.Add(hash, resName);
                Program.Log($"[GENERIC] Added {resName} (0x{hash:X16})", ConsoleColor.DarkGray);
            }
            else if (!resName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                Program.Log($"Warning: Resource file '{resName}' not found on disk!", ConsoleColor.Yellow);
            }
        }
    }

    // --- MAIN PROGRAM LOGIC ---
    class Program
    {
        private static readonly List<IResourceProcessor> processors = [new S3SAProcessor(), new STBLProcessor()];
        private static readonly IResourceProcessor genericProcessor = new GenericProcessor();

        static int Main(string[] args)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            try
            {
                var paramsMap = ParseArguments(args);
                if (!paramsMap.ContainsKey("modname") || !paramsMap.ContainsKey("dllpath"))
                {
                    Log("Error: Missing parameters -modName or -dllPath", ConsoleColor.Red);
                    Console.Beep(440, 500);
                    return 1;
                }

                string modName = paramsMap["modname"];
                string dllPath = paramsMap["dllpath"].Trim('\"');
                
                string? projectDir = paramsMap.TryGetValue("projectdir", out var pDir) ? pDir.Trim('\"') : null;
                string? resDir = null;

                if (!string.IsNullOrEmpty(projectDir))
                {
                    resDir = Path.Combine(projectDir, "resources");
                    Log($"Using explicit project directory: {projectDir}", ConsoleColor.Gray);
                }
                
                else
                {
                    string toolDir = AppDomain.CurrentDomain.BaseDirectory;
                    string? solutionDir = Directory.GetParent(toolDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
    
                    if (solutionDir != null)
                    {
                        var allResourcesDirs = Directory.GetDirectories(solutionDir, "resources", SearchOption.AllDirectories).ToList();
        
                        if (allResourcesDirs.Count == 0)
                        {
                            Log("Error: No 'resources' folder found in solution!", ConsoleColor.Red);
                            return 1;
                        }
                        else if (allResourcesDirs.Count == 1)
                        {
                            resDir = allResourcesDirs[0];
                        }
                        else
                        {
                            resDir = allResourcesDirs[0];
            
                            Log($"Warning: Multiple 'resources' folders found ({allResourcesDirs.Count})!", ConsoleColor.Yellow);
                            Console.Beep(600, 200);
                            Log("  Consider specifying -projectDir parameter to select correct folder.", ConsoleColor.Yellow);
                        }
                    }
                    else
                    {
                        Log("Error: Could not determine solution directory.", ConsoleColor.Red);
                        return 1;
                    }
                }

                if (string.IsNullOrEmpty(resDir) || !Directory.Exists(resDir))
                {
                    Log($"Error: 'resources' folder not found! (Looked in: {resDir ?? "Unknown"})", ConsoleColor.Red);
                    return 1;
                }
                
                Log($"Resource Directory: {resDir}", ConsoleColor.DarkGray);

                string? defaultPath = paramsMap.TryGetValue("defaultpath", out var tempDefaultPath) ? tempDefaultPath : null;
                string? skipFolders = paramsMap.TryGetValue("skip", out var tempSkipFolders) ? tempSkipFolders : null;

                string modsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Electronic Arts", "The Sims 3", "Mods");

                Log($"Building Package: {modName}", ConsoleColor.White);

                List<string> skipFolderList = new List<string>();
                if (!string.IsNullOrEmpty(skipFolders))
                {
                    skipFolderList = skipFolders.Split(',', ';')
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                }

                string? packagePath = FindExistingModPackage(modsDir, modName, skipFolderList);

                if (packagePath == null)
                {
                    if (!string.IsNullOrEmpty(defaultPath))
                    {
                        defaultPath = defaultPath?.Replace('/', Path.DirectorySeparatorChar)
                            .Replace('\\', Path.DirectorySeparatorChar);
                        if (defaultPath != null) packagePath = Path.Combine(modsDir, defaultPath, $"{modName}.package");
                        
                        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
                        Log($"No existing mod found. Creating new package at default path: {packagePath}",
                            ConsoleColor.Yellow);
                    }
                    else
                    {
                        string packagesDir = Path.Combine(modsDir, "Packages");
                        Directory.CreateDirectory(packagesDir);
                        packagePath = Path.Combine(packagesDir, $"{modName}.package");
                        Log($"No existing mod found. Creating new package at: {packagePath}", ConsoleColor.Yellow);
                    }
                }
                else
                {
                    if (File.Exists(packagePath))
                    {
                        Log($"Found existing mod at: {packagePath}", ConsoleColor.Yellow);
                        File.Delete(packagePath);
                        Log($"Deleted existing mod for replacement.", ConsoleColor.Yellow);
                    }
                }
                
                if (resDir != null)
                    if (packagePath != null)
                        BuildPackage(resDir, packagePath, modName, dllPath);

                Log($"[SUCCESS] Package built successfully at: {packagePath}", ConsoleColor.Green);
                Console.Beep(1000, 300);
                return 0;
            }
            catch (Exception ex)
            {
                Log($"[FATAL ERROR] {ex.Message}", ConsoleColor.Red);
                Console.Beep(440, 800);
                return 1;
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        static string? FindExistingModPackage(string modsDir, string modName, List<string> skipFolders)
        {
            try
            {
                var packageFiles = GetPackageFilesRecursive(modsDir, skipFolders);

                foreach (var file in packageFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Equals(modName, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }

                foreach (var file in packageFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    if (fileName.IndexOf(modName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Log($"Found potential matching mod: {fileName}", ConsoleColor.DarkGray);
                        return file;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log($"Warning: Error searching for existing mods: {ex.Message}", ConsoleColor.DarkYellow);
                return null;
            }
        }

        static List<string> GetPackageFilesRecursive(string directory, List<string> skipFolders)
        {
            var packageFiles = new List<string>();

            try
            {
                packageFiles.AddRange(Directory.GetFiles(directory, "*.package"));

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    string dirName = Path.GetFileName(subDir);

                    if (skipFolders.Any(skip => dirName.Equals(skip, StringComparison.OrdinalIgnoreCase)))
                    {
                        Log($"Skipping folder: {dirName}", ConsoleColor.DarkGray);
                        continue;
                    }

                    packageFiles.AddRange(GetPackageFilesRecursive(subDir, skipFolders));
                }
            }
            catch (UnauthorizedAccessException)
            {
                Log($"Warning: No access to directory: {directory}", ConsoleColor.DarkYellow);
            }
            catch (Exception ex)
            {
                Log($"Warning: Error accessing directory {directory}: {ex.Message}", ConsoleColor.DarkYellow);
            }

            return packageFiles;
        }
        
        static void BuildPackage(string resDir, string packagePath, string modName, string dllPath)
        {
            IPackage pkg = Package.NewPackage(1);
            string nameMapPath = Path.Combine(resDir, "nameMap.xml");

            if (!File.Exists(nameMapPath))
                throw new FileNotFoundException($"Could not find nameMap.xml in {resDir}");

            XDocument xml = XDocument.Load(nameMapPath);
            Dictionary<ulong, string> collectedNames = new Dictionary<ulong, string>();

            foreach (var res in xml.Descendants("resource"))
            {
                string? resName = res.Attribute("name")?.Value;
                uint resType = Convert.ToUInt32(res.Attribute("type")?.Value, 16);

                var processor = processors.FirstOrDefault(p => resName != null && p.CanHandle(resType, resName)) ??
                                genericProcessor;
                if (resName != null)
                {
                    processor.Process(pkg, resName, resType, resDir, modName, collectedNames, dllPath);
                }
            }

            WriteNameMap(pkg, collectedNames);

            pkg.SaveAs(packagePath);
            Package.ClosePackage(1, pkg);
        }

        static void WriteNameMap(IPackage pkg, Dictionary<ulong, string> collectedNames)
        {
            if (collectedNames.Count == 0) return;

            Log($"[NAMEMAP] Writing 0x0166038C with {collectedNames.Count} entries...", ConsoleColor.Cyan);

            var nameMapRes = new NameMapResource.NameMapResource(1, null);
            foreach (var kvp in collectedNames)
            {
                if (!nameMapRes.ContainsKey(kvp.Key))
                    nameMapRes.Add(kvp.Key, kvp.Value);
            }

            IResourceKey nameMapKey = new TGIBlock(1, null, Sims3Constants.Type_NameMap, 0, 0);
            pkg.AddResource(nameMapKey, nameMapRes.Stream, false);
        }

        public static string? FindFile(string dir, string name)
        {
            return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static ulong HashFNV64(string input)
        {
            input = input.ToLowerInvariant();
            ulong hash = Sims3Constants.Fnv64Offset;
            foreach (char c in input)
            {
                hash *= Sims3Constants.Fnv64Prime;
                hash ^= (byte)c;
            }

            return hash;
        }

        public static void Log(string m, ConsoleColor c)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("[ts3build] ");
            Console.ForegroundColor = c;
            Console.WriteLine(m);
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
    }
}