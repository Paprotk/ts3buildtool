using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using s3pi.Interfaces;
using s3pi.Package;
using ScriptResource;

namespace S3BuildTool
{
    // --- CONSTANTS AND MAPPINGS ---
    static class Sims3Constants
    {
        public const uint Type_S3SA = 0x073FAA07;
        public const uint Type_STBL = 0x220557DA;
        public const ulong Fnv64Prime = 0x100000001b3;
        public const ulong Fnv64Offset = 0xcbf29ce484222325;

        public static readonly Dictionary<string, byte> LocaleMap = new()
        {
            {"ENG_US", 0x00}, {"CHS_CN", 0x01}, {"CHT_CN", 0x02}, {"CZE_CZ", 0x03},
            {"DAN_DK", 0x04}, {"DUT_NL", 0x05}, {"FIN_FI", 0x06}, {"FRE_FR", 0x07},
            {"GER_DE", 0x08}, {"GRE_GR", 0x09}, {"HUN_HU", 0x0A}, {"ITA_IT", 0x0B},
            {"JPN_JP", 0x0C}, {"KOR_KR", 0x0D}, {"NOR_NO", 0x0E}, {"POL_PL", 0x0F},
            {"POR_PT", 0x10}, {"POR_BR", 0x11}, {"RUS_RU", 0x12}, {"SPA_ES", 0x13},
            {"SPA_MX", 0x14}, {"SWE_SE", 0x15}, {"THA_TH", 0x16}
        };
    }

    interface IResourceProcessor
    {
        uint TargetType { get; }
        bool CanHandle(uint type, string name);
        bool Process(IPackage pkg, string resName, uint resType, string resDir, string modName);
    }

    // --- PROCESSOR IMPLEMENTATIONS ---

    class S3SAProcessor : IResourceProcessor
    {
        public uint TargetType => Sims3Constants.Type_S3SA;
        public bool CanHandle(uint type, string name) => type == TargetType;

        public bool Process(IPackage pkg, string resName, uint resType, string resDir, string modName)
        {
            ulong hash = Program.HashFNV64(resName);
            IResourceKey key = new TGIBlock(1, null, resType, 0, hash);
            pkg.AddResource(key, new MemoryStream(), false);
            Program.Log($"[S3SA] Reserved entry for: {resName} (0x{hash:X16})", ConsoleColor.DarkCyan);
            return true;
        }
    }

    class STBLProcessor : IResourceProcessor
    {
        public uint TargetType => Sims3Constants.Type_STBL;
        public bool CanHandle(uint type, string name) => type == TargetType;

        public bool Process(IPackage pkg, string resName, uint resType, string resDir, string modName)
        {
            if (resName == "*.stbl")
            {
                ulong modBaseHash = Program.HashFNV64(modName) & 0x00FFFFFFFFFFFFFF;
                var stblFiles = Directory.GetFiles(resDir, "*.stbl", SearchOption.AllDirectories);

                if (stblFiles.Length == 0)
                    Program.Log("Warning: No .stbl files found for wildcard search!", ConsoleColor.Yellow);

                foreach (var file in stblFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    var locale = Sims3Constants.LocaleMap.FirstOrDefault(x => fileName.EndsWith(x.Key));
                    if (locale.Key != null)
                    {
                        ulong instanceId = ((ulong)locale.Value << 56) | modBaseHash;
                        pkg.AddResource(new TGIBlock(1, null, resType, 0, instanceId), new MemoryStream(File.ReadAllBytes(file)), false);
                        Program.Log($"[STBL] Injected {fileName} (0x{instanceId:X16})", ConsoleColor.DarkCyan);
                    }
                }
            }
            else
            {
                ulong hash = Program.HashFNV64(resName);
                string? file = Program.FindFile(resDir, resName);
                if (file != null)
                {
                    pkg.AddResource(new TGIBlock(1, null, resType, 0, hash), new MemoryStream(File.ReadAllBytes(file)), false);
                    Program.Log($"[STBL] Added {resName} (0x{hash:X16})", ConsoleColor.DarkCyan);
                }
                else Program.Log($"Warning: STBL file '{resName}' not found on disk!", ConsoleColor.Yellow);
            }
            return false;
        }
    }

    class GenericProcessor : IResourceProcessor
    {
        public uint TargetType => 0; 
        public bool CanHandle(uint type, string name) => true;

        public bool Process(IPackage pkg, string resName, uint resType, string resDir, string modName)
        {
            ulong hash = Program.HashFNV64(resName);
            string? file = Program.FindFile(resDir, resName);

            if (file != null)
            {
                pkg.AddResource(new TGIBlock(1, null, resType, 0, hash), new MemoryStream(File.ReadAllBytes(file)), false);
                Program.Log($"[GENERIC] Added {resName} (0x{hash:X16})", ConsoleColor.DarkGray);
            }
            else if (!resName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                Program.Log($"Warning: Resource file '{resName}' not found on disk!", ConsoleColor.Yellow);
            }
            return false;
        }
    }

    // --- MAIN PROGRAM LOGIC ---
    class Program
    {
        private static readonly List<IResourceProcessor> processors = [ new S3SAProcessor(), new STBLProcessor() ];
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
                    Console.Beep(440, 500); // Error beep
                    return 1;
                }

                string modName = paramsMap["modname"];
                string dllPath = paramsMap["dllpath"].Trim('\"');
                string toolDir = AppDomain.CurrentDomain.BaseDirectory;
                string? solutionDir = Directory.GetParent(toolDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName;
                string modsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Electronic Arts", "The Sims 3", "Mods");

                Log($"Building Package: {modName}", ConsoleColor.White);

                string packagePath = Path.Combine(modsDir, "Packages", $"{modName}.package");
                if (File.Exists(packagePath)) File.Delete(packagePath);
                
                bool s3saPlaceholderFound = false;
                if (solutionDir != null) 
                    s3saPlaceholderFound = BuildPackage(solutionDir, packagePath, modName);
                
                if (s3saPlaceholderFound)
                {
                    if (File.Exists(dllPath)) InjectDLL(packagePath, dllPath);
                    else Log($"Error: DLL file not found at {dllPath}", ConsoleColor.Red);
                }
                else
                {
                    Log("Warning: No S3SA (0x073FAA07) resource entry found in nameMap.xml. DLL injection skipped.", ConsoleColor.Yellow);
                    Console.Beep(600, 200); // Warning beep
                }

                Log("[SUCCESS] Package built successfully.", ConsoleColor.Green);
                Console.Beep(1000, 300); // Success beep
                return 0;
            }
            catch (Exception ex)
            {
                Log($"[FATAL ERROR] {ex.Message}", ConsoleColor.Red);
                Console.Beep(440, 800); // Fatal error beep
                return 1;
            }
            finally { Console.ForegroundColor = originalColor; }
        }

        static bool BuildPackage(string solutionDir, string packagePath, string modName)
        {
            string? resDir = Directory.GetDirectories(solutionDir, "resources", SearchOption.AllDirectories).FirstOrDefault();
            if (resDir == null) throw new Exception("Resources folder not found in solution.");

            IPackage pkg = Package.NewPackage(1);
            XDocument xml = XDocument.Load(Path.Combine(resDir, "nameMap.xml"));
            bool foundS3SA = false;

            foreach (var res in xml.Descendants("resource"))
            {
                string? resName = res.Attribute("name")?.Value;
                uint resType = Convert.ToUInt32(res.Attribute("type")?.Value, 16);

                var processor = processors.FirstOrDefault(p => resName != null && p.CanHandle(resType, resName)) ?? genericProcessor;
                if (resName != null)
                {
                    if (processor.Process(pkg, resName, resType, resDir, modName)) 
                        foundS3SA = true;
                }
            }

            pkg.SaveAs(packagePath);
            Package.ClosePackage(1, pkg);
            return foundS3SA;
        }

        static void InjectDLL(string pkgPath, string dllPath)
        {
            Log("Injecting DLL into S3SA resource...", ConsoleColor.Cyan);
            IPackage pkg = Package.OpenPackage(1, pkgPath, true);
            var entry = pkg.FindAll(e => e.ResourceType == Sims3Constants.Type_S3SA).FirstOrDefault();

            if (entry != null)
            {
                var script = new ScriptResource.ScriptResource(1, null);
                using (FileStream fs = File.OpenRead(dllPath))
                {
                    script.Assembly = new BinaryReader(fs);
                    pkg.ReplaceResource(entry, script);
                }
                pkg.SavePackage();
            }
            Package.ClosePackage(1, pkg);
        }

        public static string? FindFile(string dir, string name)
        {
            return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase) 
                                  || Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static ulong HashFNV64(string input)
        {
            input = input.ToLowerInvariant();
            ulong hash = Sims3Constants.Fnv64Offset;
            foreach (char c in input) { hash *= Sims3Constants.Fnv64Prime; hash ^= (byte)c; }
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