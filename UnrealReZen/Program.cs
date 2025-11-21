using CommandLine;
using CommandLine.Text;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.IO;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using Serilog;
using UnrealReZen.Core;
using UnrealReZen.Core.Helpers;

namespace UnrealReZen
{
    class Options
    {
        [Option('g', "game-dir", Required = true, HelpText = "Path to the game directory (for loading UCAS and UTOC files).")]
        public required string GameDirectory { get; set; }

        [Option('c', "content-path", Required = true, HelpText = "Path of the content that the you want to pack.")]
        public required string ContentPath { get; set; }

        [Option('e', "engine-version", Required = true, HelpText = "Unreal Engine version (e.g., GAME_UE4_0).")]
        public required string EngineVersion { get; set; }

        [Option('o', "output-path", Required = true, HelpText = "Path (including file name) for the packed utoc file.")]
        public required string OutputPath { get; set; }

        [Option('a', "aes-key", Required = false, HelpText = "AES key of the game (only if its encrypted)")]
        public string? AESKey { get; set; }

        [Option("compression-format", Required = false, Default = "Zlib", HelpText = "Compression format (None, Zlib, Oodle, LZ4).")]
        public string CompressionFormat { get; set; }

        [Option("mount-point", Required = false, Default = "../../../", HelpText = "Mount point of packed archive")]
        public string MountPoint { get; set; }

        [Option("container-id", Required = false, HelpText = "Container Id of packed archive (default is a random 8-byte number)")]
        public ulong? ContainerId { get; set; }

        [Option("game-dir-top-only", Required = false, Default = false, HelpText = "When enabled, restricts the game directory search to the top-level only.")]
        public bool GameDirTopOnly { get; set; }

        [Usage(ApplicationAlias = "UnrealReZen.exe")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return [
                     new("Making a patch for a ue5 game", new Options { GameDirectory = "C:/Games/MyGame",ContentPath = "C:/Games/MyGame/ExportedFiles", EngineVersion = "GAME_UE5_1", CompressionFormat = "Zlib", OutputPath = "C:/Games/MyGame/TestPatch_P.utoc"})
                ];
            }
        }

    }
    internal class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptionsAndReturnExitCode);
        }

        static void RunOptionsAndReturnExitCode(Options opts)
        {
            Constants.ToolDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!OodleHelper.DownloadOodleDll(Path.Combine(Constants.ToolDirectory, OodleHelper.OODLE_DLL_NAME)))
            {
                Log.Fatal("UnrealReZen failed to download the oodle dll. please check you internet connection or place oo2core_9_win64.dll in the tool directory");
                Console.ReadLine();
                return;
            }
            if (!ZlibHelper.DownloadDll(Path.Combine(Constants.ToolDirectory, ZlibHelper.DLL_NAME)))
            {
                Log.Fatal("UnrealReZen failed to download the zlib dll. please check you internet connection or place zlib-ng2.dll in the tool directory");
                Console.ReadLine();
                return;
            }
            if (!Enum.TryParse(typeof(EGame), opts.EngineVersion, out var engineVersion))
            {
                Log.Fatal("Invalid Unreal Engine version. Please enter a valid version (e.g., GAME_UE4_0).");
                Log.Information("List of supported engine versions: " + string.Join("\n", Enum.GetNames(typeof(EGame))));
                return;
            }
            if (!Constants.CompressionTypes.Contains(opts.CompressionFormat.ToLower()))
            {
                Log.Fatal($"Unsupported compression format : {opts.CompressionFormat}");
                return;
            }
            if (Path.GetExtension(opts.OutputPath) != ".utoc")
            {
                Log.Fatal($"Output path must contains utoc extension");
                return;
            }

            Console.WriteLine($"Game Directory: {opts.GameDirectory}");
            Console.WriteLine($"Content Path: {opts.ContentPath}");
            Console.WriteLine($"Unreal Engine Version: {engineVersion}");
            Console.WriteLine($"Output Path: {opts.OutputPath}");

            var aesKey = new FAesKey(opts.AESKey ?? Constants.DefaultAES);
            DefaultFileProvider provider;

            Log.Information("Loading Game Archives...");
            try
            {
                var searchOption = opts.GameDirTopOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
                provider = new DefaultFileProvider(opts.GameDirectory, searchOption, true, new VersionContainer((EGame)engineVersion));
                provider.Initialize();
                provider.SubmitKey(new FGuid(), aesKey);
                provider.LoadLocalization(ELanguage.English);

                if (provider.RequiredKeys.Count > 0 && provider.Keys.Count == 0)
                {
                    Log.Fatal("Some of archives needs AES Key. Please enter aes key");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Fatal("Error:" + ex.ToString());
                Log.Information("Maybe changing aes key or engine version helps");
                return;
            }

            foreach (var vfs in provider.MountedVfs)
            {
                vfs.Dispose();
            }

            Log.Information("Packing Contents...");
            Dependency m = new() { Deps = new DependenciesData { ChunkIDToDependencies = [] }, Files = [] };
            List<string> FilesToRepack = new(Directory.GetFiles(opts.ContentPath, "*", SearchOption.AllDirectories));
            var newContainerID = opts.ContainerId ?? CryptographyHelpers.RandomUlong();

            byte type = (EGame)engineVersion >= EGame.GAME_UE5_0 ? (byte)EIoChunkType5.ContainerHeader : (byte)EIoChunkType.ContainerHeader;
            m.Files.Add(new ManifestFile { ChunkID = new FIoChunkID(newContainerID, 0, 0, type), Filepath = Constants.DepFileName });
            m.Deps.ThisPackageID = newContainerID;
            if (FilesToRepack.Count == 0)
            {
                Log.Fatal("No valid files found in the content path");
                return;
            }
            foreach (var file in FilesToRepack)
            {
                string filename = file.Replace(opts.ContentPath + "\\", "").Replace("\\", "/");
                Log.Information("Mounting " + Path.GetFileName(filename));
                var filedata = provider.Files.Values.Where(a => Path.GetExtension(((AbstractVfsReader)((VfsEntry)a).Vfs).Name) == ".utoc" && a.Path.Equals(filename, StringComparison.CurrentCultureIgnoreCase));
                if (filedata == null || !filedata.Any())
                {
                    Log.Warning("Skipping " + filename + " because its not found in archives.");
                    continue;
                }
                foreach (var dep in filedata)
                {
                    dynamic b = dep;
                    FIoChunkId c = b.ChunkId;
                    m.Files.Add(new ManifestFile { Filepath = dep.Path, ChunkID = new FIoChunkID(c.ChunkId, 0, 0, c.ChunkType) });
                    IoStoreReader IoFile = b.IoStoreReader;
                    if (IoFile.ContainerHeader != null)
                    {
                        foreach (var st in IoFile.ContainerHeader.StoreEntries)
                        {
                            if (m.Deps.ChunkIDToDependencies.ContainsKey(c.ChunkId)) continue;
                            m.Deps.ChunkIDToDependencies.Add(c.ChunkId, st);
                        }
                    }
                }
            }
            Log.Information("Packing files...");
            Packer.PackToCasToc(opts.ContentPath, m, opts.OutputPath, opts.CompressionFormat, aesKey, opts.MountPoint, (EGame)engineVersion);
            Console.WriteLine($"Done! {FilesToRepack.Count} file(s) packed");

        }
    }

}
