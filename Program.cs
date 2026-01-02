using s3pi.Interfaces;
using s3pi.Package;

class S3BuildTool
{
    static int Main(string[] args)
    {
        ConsoleColor originalColor = Console.ForegroundColor;

        try
        {
            if (args.Length < 2)
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine("Usage: ts3buildtool.exe <modName> <config>");
                Console.ForegroundColor = originalColor;
                return 1;
            }

            string modName = args[0];
            string config = args[1];

            string toolDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? solutionDirInfo = Directory.GetParent(toolDir.TrimEnd(Path.DirectorySeparatorChar));

            if (solutionDirInfo == null)
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine("[ERROR] Could not determine Solution Root.");
                Console.ForegroundColor = originalColor;
                return 1;
            }

            string solutionDir = solutionDirInfo.FullName;

            Console.WriteLine("--- Sims 3 Build Tool ---");

            var binFolders = Directory.GetDirectories(solutionDir, "bin", SearchOption.AllDirectories)
                .SelectMany(bin => Directory.GetDirectories(bin, config))
                .ToList();

            FileInfo? newestDll = null;
            foreach (var folder in binFolders)
            {
                var dlls = new DirectoryInfo(folder).GetFiles("*.dll")
                    .Where(f => !f.Name.StartsWith("System.") && !f.Name.StartsWith("Microsoft.") &&
                                !f.Name.StartsWith("s3pi."))
                    .OrderByDescending(f => f.LastWriteTime).ToList();

                if (dlls.Count > 0 && (newestDll == null || dlls[0].LastWriteTime > newestDll.LastWriteTime))
                    newestDll = dlls[0];
            }

            if (newestDll == null)
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine($"[ERROR] Could not find any compiled DLL for config '{config}'.");
                Console.ForegroundColor = originalColor;
                return 1;
            }

            string dllPath = newestDll.FullName;
            Console.WriteLine($"[S3Build] Selected Source DLL: {newestDll.Name}");

            string modsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Electronic Arts", "The Sims 3", "Mods");

            string? pkgPath = Directory.GetFiles(modsDir, $"{modName}.package", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (pkgPath == null)
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine($"[ERROR] Could not find {modName}.package in Mods folder!");
                Console.ForegroundColor = originalColor;
                return 1;
            }

            InjectS3SA(pkgPath, dllPath);

            SetColor(ConsoleColor.Green);
            Console.WriteLine("[SUCCESS] Mod updated successfully.");
            Console.Beep(880, 200);

            Console.ForegroundColor = originalColor;
            return 0;
        }
        catch (Exception ex)
        {
            SetColor(ConsoleColor.Red);
            Console.WriteLine("***************************************");
            Console.WriteLine($"[FATAL ERROR] {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine("***************************************");
            Console.ForegroundColor = originalColor;
            return 1;
        }
    }

    static void InjectS3SA(string pkgPath, string dllPath)
    {
        IPackage pkg = Package.OpenPackage(1, pkgPath, true);
        var s3saEntries = pkg.FindAll(e => e.ResourceType == 0x073FAA07);

        if (s3saEntries.Count == 0)
        {
            Console.WriteLine("INFO: No S3SA found in package. Nothing to replace.");
        }
        else
        {
            var entry = s3saEntries[0];
            var scriptResource = new ScriptResource.ScriptResource(1, null);

            using (FileStream fs = File.OpenRead(dllPath))
            {
                scriptResource.Assembly = new BinaryReader(fs);
                pkg.ReplaceResource(entry, scriptResource);
            }

            pkg.SavePackage();
            Console.WriteLine($"[S3Build] Replaced S3SA content successfully.");
        }

        Package.ClosePackage(1, pkg);
    }

    static void SetColor(ConsoleColor color)
    {
        Console.ForegroundColor = color;
    }
}