using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vividstasis_Text_Editor
{
    internal class GeneralInfo
    {
        bool IsDebuggerDisabled = true;
        public byte BytecodeVersion = 0x10;
        public ushort Unknown = 0;
        public string FileName = "VIVIDSTASIS";
        public string Config = "Release";
        public uint LastObj = 10000;
        public uint LastTitle = 10000000;
        public uint GameID = 13371337;
        public int DirectPlayGuid = 0;
        public string Name = "VIVIDSTASIS";
        public uint Major = 2;
        public uint Minor = 0;
        public uint Release = 0;
        public uint Build = 1337;
        public uint DefaultWindowWidth = 1024;
        public uint DefaultWindowHeight = 768;
        public uint Info;
        public byte[] LicenseMD5;
        public uint LicenseCRC32;
        public ulong Timestamp;
        public string DisplayName = "vivid/stasis";
        public ulong FunctionClassifications;
        public uint SteamAppID;
    }
}
