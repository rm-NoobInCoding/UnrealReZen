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
    internal class Options
    {
        [Option('g', "game-dir", Required = true, HelpText = "Path to the game directory (for loading UCAS and UTOC files).")]
        public required string GameDirectory { get; set; }

        [Option('c', "content-path", Required = true, HelpText = "Path of the content that the you want to pack.")]
        public required string ContentPath { get; set; }

        [Option('e', "engine-version", Required = true, HelpText = "Unreal Engine version (e.g., GAME_UE4_0).")]
        public required string EngineVersion { get; set; }

        [Option('o', "output-path", Required = true, HelpText = "Path (including file name) for the packed utoc file.")]
        public required string OutputPath { get; set; }

        [Option('a', "aes-key", Required = false, HelpText = "AES key for reading the game's encrypted source archives. Not used to encrypt the output unless --encrypt-output is also set.")]
        public string? AESKey { get; set; }

        [Option("encrypt-output", Required = false, Default = false, HelpText = "Encrypt the generated .ucas with the game's AES key and set the EncryptedContainerFlag in the .utoc. Most games accept plain archives for mods; leave off unless you know the target refuses unencrypted containers.")]
        public bool EncryptOutput { get; set; }

        [Option("compression-format", Required = false, Default = "Zlib", HelpText = "Compression format (None, Zlib, Oodle, LZ4).")]
        public string CompressionFormat { get; set; } = "Zlib";

        [Option("mount-point", Required = false, Default = "../../../", HelpText = "Mount point of packed archive")]
        public string MountPoint { get; set; } = "../../../";

        [Option("container-id", Required = false, HelpText = "Container Id of packed archive (default: CityHash64 of the lowercased output file name, matching Unreal's FIoContainerId::FromName).")]
        public ulong? ContainerId { get; set; }

        [Option("game-dir-top-only", Required = false, Default = false, HelpText = "When enabled, restricts the game directory search to the top-level only.")]
        public bool GameDirTopOnly { get; set; }

        [Usage(ApplicationAlias = "UnrealReZen.exe")]
        public static IEnumerable<Example> Examples => [
            new("Making a patch for a ue5 game", new Options
            {
                GameDirectory = "C:/Games/MyGame",
                ContentPath = "C:/Games/MyGame/ExportedFiles",
                EngineVersion = "GAME_UE5_1",
                CompressionFormat = "Zlib",
                OutputPath = "C:/Games/MyGame/TestPatch_P.utoc"
            })
        ];
    }

    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitError = 1;

        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            int exitCode = ExitError;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => exitCode = Run(opts));
            return exitCode;
        }

        private static int Run(Options opts)
        {
            Constants.ToolDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (!EnsureNativeDlls()) return ExitError;
            if (!TryParseEngineVersion(opts.EngineVersion, out var engineVersion)) return ExitError;
            if (!ValidateCliOptions(opts)) return ExitError;

            LogOptionsSummary(opts, engineVersion);

            var aesKey = new FAesKey(opts.AESKey ?? Constants.DefaultAES);
            if (!TryLoadProvider(opts, engineVersion, aesKey, out var provider)) return ExitError;

            foreach (var vfs in provider.MountedVfs)
            {
                vfs.Dispose();
            }

            var filesToRepack = Directory.GetFiles(opts.ContentPath, "*", SearchOption.AllDirectories);
            if (filesToRepack.Length == 0)
            {
                Log.Fatal("No valid files found in the content path");
                return ExitError;
            }

            Log.Information("Packing Contents...");
            var manifest = BuildManifest(provider, opts, engineVersion, filesToRepack);

            Log.Information("Packing files...");
            var outputAesKey = opts.EncryptOutput ? aesKey : null;
            Packer.PackToCasToc(opts.ContentPath, manifest, opts.OutputPath, opts.CompressionFormat, outputAesKey, opts.MountPoint, engineVersion);
            Console.WriteLine($"Done! {filesToRepack.Length} file(s) packed");
            return ExitOk;
        }

        private static bool EnsureNativeDlls()
        {
            try
            {
                OodleHelper.Initialize(Path.Combine(Constants.ToolDirectory, OodleHelper.OodleFileName));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"Failed to initialize Oodle. Place {OodleHelper.OodleFileName} in the tool directory and try again.");
                return false;
            }
            if (OodleHelper.Instance is null)
            {
                Log.Fatal($"Oodle was not registered. Place {OodleHelper.OodleFileName} in the tool directory or check your internet connection.");
                return false;
            }

            try
            {
                ZlibHelper.Initialize(Path.Combine(Constants.ToolDirectory, ZlibHelper.DllName));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"Failed to initialize Zlib. Place {ZlibHelper.DllName} in the tool directory and try again.");
                return false;
            }
            if (ZlibHelper.Instance is null)
            {
                Log.Fatal($"Zlib was not registered. Place {ZlibHelper.DllName} in the tool directory or check your internet connection.");
                return false;
            }
            return true;
        }

        private static bool TryParseEngineVersion(string raw, out EGame engineVersion)
        {
            if (Enum.TryParse(raw, out engineVersion) && Enum.IsDefined(engineVersion))
            {
                return true;
            }
            Log.Fatal("Invalid Unreal Engine version. Please enter a valid version (e.g., GAME_UE4_0).");
            Log.Information("List of supported engine versions: " + string.Join("\n", Enum.GetNames<EGame>()));
            return false;
        }

        private static bool ValidateCliOptions(Options opts)
        {
            if (!Constants.CompressionTypes.Contains(opts.CompressionFormat.ToLowerInvariant()))
            {
                Log.Fatal($"Unsupported compression format : {opts.CompressionFormat}");
                return false;
            }
            if (!string.Equals(Path.GetExtension(opts.OutputPath), ".utoc", StringComparison.OrdinalIgnoreCase))
            {
                Log.Fatal("Output path must contain utoc extension");
                return false;
            }
            return true;
        }

        private static void LogOptionsSummary(Options opts, EGame engineVersion)
        {
            Console.WriteLine($"Game Directory: {opts.GameDirectory}");
            Console.WriteLine($"Content Path: {opts.ContentPath}");
            Console.WriteLine($"Unreal Engine Version: {engineVersion}");
            Console.WriteLine($"Output Path: {opts.OutputPath}");
        }

        private static bool TryLoadProvider(Options opts, EGame engineVersion, FAesKey aesKey, out DefaultFileProvider provider)
        {
            Log.Information("Loading Game Archives...");
            try
            {
                var searchOption = opts.GameDirTopOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
                provider = new DefaultFileProvider(opts.GameDirectory, searchOption, new VersionContainer(engineVersion), StringComparer.OrdinalIgnoreCase);
                provider.Initialize();
                provider.SubmitKey(new FGuid(), aesKey);

                if (provider.RequiredKeys.Count > 0 && provider.Keys.Count == 0)
                {
                    Log.Fatal("Some archives require an AES key. Please provide --aes-key.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Fatal("Error: " + ex);
                Log.Information("Maybe changing aes key or engine version helps");
                provider = null!;
                return false;
            }
        }

        private static Dependency BuildManifest(DefaultFileProvider provider, Options opts, EGame engineVersion, string[] filesToRepack)
        {
            var newContainerId = opts.ContainerId ?? DeriveContainerIdFromName(opts.OutputPath);
            byte containerChunkType = engineVersion >= EGame.GAME_UE5_0
                ? (byte)EIoChunkType5.ContainerHeader
                : (byte)EIoChunkType.ContainerHeader;

            var manifest = new Dependency
            {
                Deps = new DependenciesData
                {
                    ThisPackageID = newContainerId,
                    ChunkIDToDependencies = []
                },
                Files = [new ManifestFile
                {
                    ChunkID = new FIoChunkID(newContainerId, 0, 0, containerChunkType),
                    Filepath = Constants.DepFileName
                }]
            };

            var utocEntryLookup = BuildUtocEntryLookup(provider);
            var seenChunks = new HashSet<(ulong id, byte type)> { (newContainerId, containerChunkType) };

            foreach (var file in filesToRepack)
            {
                string filename = Path.GetRelativePath(opts.ContentPath, file).Replace('\\', '/');
                Log.Information("Mounting " + Path.GetFileName(filename));
                if (!utocEntryLookup.TryGetValue(filename, out var matches))
                {
                    Log.Warning("Skipping " + filename + " because it's not found in archives.");
                    continue;
                }
                foreach (var entry in matches)
                {
                    var chunkId = entry.ChunkId;
                    if (!seenChunks.Add((chunkId.ChunkId, chunkId.ChunkType))) continue;

                    manifest.Files.Add(new ManifestFile
                    {
                        Filepath = entry.Path,
                        ChunkID = new FIoChunkID(chunkId.ChunkId, 0, 0, chunkId.ChunkType)
                    });

                    var header = entry.IoStoreReader.ContainerHeader;
                    if (header != null)
                    {
                        foreach (var storeEntry in header.StoreEntries)
                        {
                            manifest.Deps.ChunkIDToDependencies.TryAdd(chunkId.ChunkId, storeEntry);
                        }
                    }
                }
            }
            return manifest;
        }

        private static ulong DeriveContainerIdFromName(string outputPath)
        {
            var name = Path.GetFileNameWithoutExtension(outputPath);
            return FPackageId.FromName(name).id;
        }

        private static Dictionary<string, List<FIoStoreEntry>> BuildUtocEntryLookup(DefaultFileProvider provider)
        {
            var lookup = new Dictionary<string, List<FIoStoreEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in provider.Files.Values.OfType<FIoStoreEntry>())
            {
                if (Path.GetExtension(((AbstractVfsReader)entry.Vfs).Name) != ".utoc") continue;
                if (!lookup.TryGetValue(entry.Path, out var list))
                {
                    list = new List<FIoStoreEntry>();
                    lookup[entry.Path] = list;
                }
                list.Add(entry);
            }
            return lookup;
        }
    }
}
