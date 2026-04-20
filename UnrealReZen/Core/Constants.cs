namespace UnrealReZen.Core
{
    public static class Constants
    {
        public static readonly string[] CompressionTypes = ["none", "zlib", "oodle", "lz4"];
        public static readonly string DefaultAES = "0x" + new string('0', 64);

        public static string ToolDirectory = "";
        public static string MountPoint = "../../../";

        public const string MagicUtoc = "-==--==--==--==-";
        public const string DepFileName = "dependencies";
        public const uint NoneEntry = 0xFFFFFFFF;
        public const int UE5_DepFile_Sig = 1232028526;
        public const int CompSize = 0x10000;
        public const int PackUtocVersion = 5;
        public const int CompressionNameLength = 32;
    }
}
