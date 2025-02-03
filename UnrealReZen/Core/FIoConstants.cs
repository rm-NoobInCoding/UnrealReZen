namespace UnrealReZen.Core
{
    public static class Constants
    {
        public static string[] CompressionTypes = ["none", "zlib", "oodle", "lz4"];
        public static string ToolDirectory = "";
        public static string DefaultAES = "0x" + new string('0', 64);
        public static string MountPoint = "../../../";
        public const string MagicUtoc = "-==--==--==--==-";
        public const string DepFileName = "dependencies";
        public const string UnrealSignature = "\xC1\x83\x2A\x9E";
        public const uint NoneEntry = 0xFFFFFFFF;
        public const int UE5_DepFile_Sig = 1232028526;
        public const int CompSize = 0x10000;
        public const int PackUtocVersion = 3;
        public const int CompressionNameLength = 32;
    }
}
