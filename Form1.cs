using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using System.Diagnostics;
using System.Runtime.Remoting.Lifetime;
using System.Reflection;
using System.IO.Compression;

namespace vividstasis_Text_Editor
{
    public partial class Form1 : Form
    {
        public enum DataType : byte
        {
            Double,
            Float,
            Int32,
            Int64,
            Boolean,
            Variable,
            String,
            [Obsolete("Unused")]
            Instance,
            Delete, // these 3 types apparently exist
            Undefined,
            UnsignedInt,
            Int16 = 0x0f
        }
        public enum InstructionType
        {
            SingleTypeInstruction,
            DoubleTypeInstruction,
            ComparisonInstruction,
            GotoInstruction,
            PushInstruction,
            PopInstruction,
            CallInstruction,
            BreakInstruction
        }

        public enum Opcode : byte
        {
            Conv = 0x07, // Push((Types.Second)Pop) // DoubleTypeInstruction
            Mul = 0x08, // Push(Pop() * Pop()) // DoubleTypeInstruction
            Div = 0x09, // Push(Pop() / Pop()) // DoubleTypeInstruction
            Rem = 0x0A, // Push(Remainder(Pop(), Pop())) // DoubleTypeInstruction
            Mod = 0x0B, // Push(Pop() % Pop()) // DoubleTypeInstruction
            Add = 0x0C, // Push(Pop() + Pop()) // DoubleTypeInstruction
            Sub = 0x0D, // Push(Pop() - Pop()) // DoubleTypeInstruction
            And = 0x0E, // Push(Pop() & Pop()) // DoubleTypeInstruction
            Or = 0x0F, // Push(Pop() | Pop()) // DoubleTypeInstruction
            Xor = 0x10, // Push(Pop() ^ Pop()) // DoubleTypeInstruction
            Neg = 0x11, // Push(-Pop()) // SingleTypeInstruction
            Not = 0x12, // Push(~Pop()) // SingleTypeInstruction
            Shl = 0x13, // Push(Pop() << Pop()) // DoubleTypeInstruction
            Shr = 0x14, // Push(Pop() >>= Pop()) // DoubleTypeInstruction
            Cmp = 0x15, // Push(Pop() `cmp` Pop())// ComparisonInstruction
            Pop = 0x45, // Instance.Destination = Pop();
            Dup = 0x86, // Push(Peek()) // SingleTypeInstruction
            Ret = 0x9C, // return Pop() // SingleTypeInstruction
            Exit = 0x9D, // return; // SingleTypeInstruction
            Popz = 0x9E, // Pop(); // SingleTypeInstruction
            B = 0xB6, // goto Index + Offset*4; // GotoInstruction
            Bt = 0xB7, // if (Pop()) goto Index + Offset*4; // GotoInstruction
            Bf = 0xB8, // if (!Pop()) goto Index + Offset*4; // GotoInstruction
            PushEnv = 0xBA, // GotoInstruction
            PopEnv = 0xBB, // GotoInstruction
            Push = 0xC0, // Push(Value) // push constant
            PushLoc = 0xC1, // Push(Value) // push local
            PushGlb = 0xC2, // Push(Value) // push global
            PushBltn = 0xC3, // Push(Value) // push builtin variable
            PushI = 0x84, // Push(Value) // push int16
            Call = 0xD9, // Function(arg0, arg1, ..., argn) where arg = Pop() and n = ArgumentsCount
            CallV = 0x99, // TODO: Unknown, maybe to do with calling using the stack? Generates with "show_message((function(){return 5;})());"
            Break = 0xFF, // TODO: Several sub-opcodes in GMS 2.3
        }
        [Flags]
        public enum AudioEntryFlags : uint
        {
            /// <summary>
            /// Whether the sound is embedded into the data file.
            /// </summary>
            /// <remarks>This should ideally be used for sound effects, but not for music.<br/>
            /// The GameMaker documentation also calls this "not streamed" (or "from memory") for when the flag is present,
            /// or "streamed" when it isn't.</remarks>
            IsEmbedded = 0x1,
            /// <summary>
            /// Whether the sound is compressed.
            /// </summary>
            /// <remarks>When a sound is compressed it will take smaller memory/disk space.
            /// However, this is at the cost of needing to decompress it when it needs to be played,
            /// which means slightly higher CPU usage.</remarks>
            // TODO: where exactly is this used? for non-embedded compressed files, this flag doesnt seem to be set.
            IsCompressed = 0x2,
            /// <summary>
            /// Whether the sound is decompressed on game load.
            /// </summary>
            /// <remarks>When a sound is played, it must be loaded into memory first, which would usually be done when the sound is first used.
            /// If you preload it, the sound will be loaded into memory at the start of the game.</remarks>
            // TODO: some predecessor/continuation of Preload? Also why is this flag the combination of both compressed and embedded?
            IsDecompressedOnLoad = 0x3,
            /// <summary>
            /// Whether this sound uses the "new audio system".
            /// </summary>
            /// <remarks>This is default for everything post Game Maker: Studio.
            /// The legacy sound system was used in pre Game Maker 8.</remarks>
            Regular = 0x64,
        }
        List<string> allChunkNames = new List<string>();
        List<string> vividStasisStrings = new List<string>();
        //The string, and then the pointer of the string.
        Dictionary<string, uint> stringPointers = new Dictionary<string, uint>();
        //Stores position and then what the string pointer is.
        List<StringReference> readStringReferences = new List<StringReference>();
        List<string> storyStrings = new List<string>();
        List<string> gameStrings = new List<string>();
        List<string> unknownStrings = new List<string>();
        static GeneralInfo info = new GeneralInfo();
        static TreeNode selectedNode = null;
        Loading load = new Loading();
        string origString;
        string fileName { get { return info.FileName; } }
        string name { get { return info.Name; } }
        string displayName { get { return info.DisplayName; } }
        uint major { get { return info.Major; } }
        uint minor { get { return info.Minor; } }
        uint release { get { return info.Release; } }
        uint build { get { return info.Build; } }
        public static bool IsVersionAtLeast(uint major, uint minor = 0, uint release = 0, uint build = 0)
        {
            if (info.Major != major)
                return (info.Major > major);

            if (info.Minor != minor)
                return (info.Minor > minor);

            if (info.Release != release)
                return (info.Release > release);

            if (info.Build != build)
                return (info.Build > build);
            return false;
        }
        public static void SetVersion(uint major = 0, uint minor = 0, uint release = 0, uint build = 0)
        {
            info.Major = major;
            info.Minor = minor;
            info.Release = release;
            info.Build = build;
        }
        public void Unserialize(string chunkName,BinaryReader br,uint jump, long savePos,bool writing = false)
        {
            if(!writing)
            {
                EditLoadingMessage($"Reading chunk {chunkName}");
            } else
            {
                EditLoadingMessage($"Writing chunk {chunkName}");
            }
            
            switch(chunkName)
            {
                case "GEN8":
                    //Get general info
                    //IsDebuggerDisabled
                    br.ReadByte();
                    //Bytecode Version
                    info.BytecodeVersion = br.ReadByte();
                    //Unknown
                    info.Unknown = br.ReadUInt16();
                    info.FileName = ReadVividString(br);
                    //Config
                    info.Config = ReadVividString(br);
                    //LastObj
                    info.LastObj = br.ReadUInt32();
                    //LastTitle
                    info.LastTitle = br.ReadUInt32();
                    //GameID
                    info.GameID = br.ReadUInt32();
                    //guidData
                    br.ReadBytes(16);
                    //Can't really see a difference between filename and this, but it's nice to have.   
                    info.Name = ReadVividString(br);
                    info.Major = br.ReadUInt32();
                    info.Minor = br.ReadUInt32();
                    info.Release = br.ReadUInt32();
                    info.Build = br.ReadUInt32();
                    //DefaultWindowWidth
                    info.DefaultWindowWidth = br.ReadUInt32();
                    //DefaultWindowHeight
                    info.DefaultWindowHeight = br.ReadUInt32();
                    //Info
                    info.Info = br.ReadUInt32();
                    //License CRC32
                    info.LicenseCRC32 = br.ReadUInt32();
                    //License MD5
                    info.LicenseMD5 = br.ReadBytes(16);
                    //Timestamp
                    info.Timestamp = br.ReadUInt64();
                    info.DisplayName = ReadVividString(br);
                    //Function classifications
                    br.ReadUInt64();
                    //SteamAppID
                    br.ReadInt32();
                    //No more strings at this point, jump back to beginning of file. (TODO: There are more strings)
                    return;
                case "OPTN":
                    //Unknown 1
                    br.ReadUInt32();
                    //Unknown 2
                    br.ReadUInt32();
                    //Info
                    br.ReadUInt64();
                    //Scale
                    br.ReadUInt32();
                    //WindowColor
                    br.ReadUInt32();
                    //ColorDepth
                    br.ReadUInt32();
                    //Resolution
                    br.ReadUInt32();
                    //Frequency
                    br.ReadUInt32();
                    //VertexSync
                    br.ReadUInt32();
                    //Priority
                    br.ReadUInt32();
                    //BackImage
                    br.ReadUInt32();
                    //FrontImage
                    br.ReadUInt32();
                    //LoadImage
                    br.ReadUInt32();
                    //LoadAlpha
                    br.ReadUInt32();
                    br.ReadUInt32();
                    for (int i = 0; i < 7; i++)
                    {   //Name
                        ReadVividString(br);
                        //Value
                        ReadVividString(br);
                    }
                    break;
                case "LANG":
                    //Unknown1
                    br.ReadUInt32();
                    uint LanguageCount = br.ReadUInt32();
                    uint EntryCount = br.ReadUInt32();
                    // Read the identifiers for each entry
                    for (int i = 0; i < EntryCount; i++)
                    {
                        //Read Entry IDS
                        ReadVividString(br);
                    }
                    for(int i = 0; i < LanguageCount; i++)
                    {
                        //Name
                        ReadVividString(br);
                        //Region
                        ReadVividString(br);
                        for(uint i2 = 0; i2 < EntryCount; i2++)
                        {
                            //Read Language Entry
                            ReadVividString(br);
                        }
                    }
                    break;
                case "EXTN":
                    uint extensionCount = br.ReadUInt32();
                    long extensionPos;
                    long fileListPointerListPos;
                    long optionListPointerListPos;
                    for (int i = 0; i < extensionCount; i++)
                    {
                        uint extensionPointer = br.ReadUInt32();
                        extensionPos = br.BaseStream.Position;
                        br.BaseStream.Position = extensionPointer;
                        //FolderName
                        ReadVividString(br);
                        //Name
                        ReadVividString(br);
                        //Version
                        ReadVividString(br);
                        //ClassName
                        ReadVividString(br);
                        //ExtensionFilePointer3
                        uint fileListPointer = br.ReadUInt32();
                        fileListPointerListPos = br.BaseStream.Position;
                        uint optionListPointer = br.ReadUInt32();
                        optionListPointerListPos = br.BaseStream.Position;
                        br.BaseStream.Position = fileListPointer;
                        uint fileCount = br.ReadUInt32();
                        br.BaseStream.Position = optionListPointer;
                        uint optionCount = br.ReadUInt32();
                        br.BaseStream.Position = fileListPointer;
                        //Read file count
                        br.ReadUInt32();
                        for (int i2 = 0; i2 < fileCount; i2++)
                        {
                            //Pointer to the string pointer
                            uint filePointer = br.ReadUInt32();
                            fileListPointerListPos = br.BaseStream.Position;
                            br.BaseStream.Position = filePointer;
                            //FileName
                            ReadVividString(br);
                            //CleanupScript
                            ReadVividString(br);
                            //InitScript
                            ReadVividString(br);
                            //ExtensionKind
                            br.ReadUInt32();
                            uint functioneCount = br.ReadUInt32();
                            long functionListJump = br.BaseStream.Position;
                            long functionPos = 0;
                            for(int i3 = 0; i3 < functioneCount; i3++)
                            {
                                uint functionPointer = br.ReadUInt32();
                                functionPos = br.BaseStream.Position;
                                br.BaseStream.Position = functionPointer;
                                //Name
                                ReadVividString(br);
                                //ID
                                br.ReadUInt32();
                                //Kind
                                br.ReadUInt32();
                                //RetType
                                br.ReadUInt32();
                                //ExtName
                                ReadVividString(br);
                                br.BaseStream.Position = functionPos;
                            }
                            br.BaseStream.Position = fileListPointerListPos;
                        }
                        br.BaseStream.Position = optionListPointer;
                        //Read option count
                        br.ReadUInt32();
                        for (int i3 = 0; i3 < optionCount; i3++)
                        {
                            uint optionPointer = br.ReadUInt32();
                            optionListPointerListPos = br.BaseStream.Position;
                            br.BaseStream.Position = optionPointer;
                            //Name
                            ReadVividString(br);
                            //Value
                            ReadVividString(br);
                            br.BaseStream.Position = optionListPointerListPos;
                        }
                        br.BaseStream.Position = extensionPos;

                    }
                    break;
                case "SOND":
                    uint soundCount = br.ReadUInt32();
                    long pos = 0;
                    for(int i = 0; i < soundCount; i++)
                    {
                        uint soundPointer = br.ReadUInt32();
                        pos = br.BaseStream.Position;
                        br.BaseStream.Position = soundPointer;
                        //Name
                        ReadVividString(br);
                        //Flags
                        AudioEntryFlags Flags = (AudioEntryFlags)br.ReadUInt32();
                        //Type
                        ReadVividString(br);
                        //File
                        ReadVividString(br);
                        //Effects
                        br.ReadUInt32();
                        //Volume
                        br.ReadSingle();
                        //Pitch
                        br.ReadSingle();
                        br.BaseStream.Position = pos;
                    }
                    break;
                case "AGRP":
                    uint groupCount = br.ReadUInt32();
                    long groupPos;
                    for(int i = 0; i < groupCount; i++)
                    {
                        uint groupPointer = br.ReadUInt32();
                        groupPos = br.BaseStream.Position;
                        br.BaseStream.Position = groupPointer;
                        //Name
                        ReadVividString(br);
                        br.BaseStream.Position = groupPos;
                    }
                    break;
                case "SPRT":
                    uint spriteCount = br.ReadUInt32();
                    long spritePos;
                    for (int i = 0; i < spriteCount; i++)
                    {
                        uint spritePointer = br.ReadUInt32();
                        spritePos = br.BaseStream.Position;
                        br.BaseStream.Position = spritePointer;
                        //Name
                        ReadVividString(br);
                        //Check if anything else is actually nesseceary, nothing so far
                        br.BaseStream.Position = spritePos;
                    }
                    break;
                case "BGND":
                    uint backgroundCount = br.ReadUInt32();
                    long backgroundPos;
                    for (int i = 0; i < backgroundCount; i++)
                    {
                        uint backgroundPointer = br.ReadUInt32();
                        backgroundPos = br.BaseStream.Position;
                        br.BaseStream.Position = backgroundPointer;
                        //Name
                        ReadVividString(br);
                        //Check if anything else is actually nesseceary, nothing so far
                        br.BaseStream.Position = backgroundPos;
                    }
                    break;
                case "PATH":
                    uint pathCount = br.ReadUInt32();
                    long pathPos;
                    for (int i = 0; i < pathCount; i++)
                    {
                        uint pathPointer = br.ReadUInt32();
                        pathPos = br.BaseStream.Position;
                        br.BaseStream.Position = pathPointer;
                        //Name
                        ReadVividString(br);
                        //Check if anything else is actually nesseceary, nothing so far
                        br.BaseStream.Position = pathPos;
                    }
                    break;
                case "SCPT":
                    uint scriptCount = br.ReadUInt32();
                    long scriptPos;
                    for (int i = 0; i < scriptCount; i++)
                    {
                        uint scriptPointer = br.ReadUInt32();
                        scriptPos = br.BaseStream.Position;
                        br.BaseStream.Position = scriptPointer;
                        //Name
                        ReadVividString(br);
                        //Check if anything else is actually nesseceary, nothing so far (CODE reference)
                        br.BaseStream.Position = scriptPos;
                    }
                    break;
                case "GLOB":
                    //No strings so ignored
                    break;
                case "GMEN":
                    //No strings so ignored
                    break;
                case "SHDR":
                    uint shaderCount = br.ReadUInt32();
                    long shaderPos;
                    for (int i = 0; i < shaderCount; i++)
                    {
                        uint shaderPointer = br.ReadUInt32();
                        shaderPos = br.BaseStream.Position;
                        br.BaseStream.Position = shaderPointer;
                        //Name
                        ReadVividString(br);
                        //Type
                        br.ReadUInt32();
                        //GLSL_ES_Vertex
                        ReadVividString(br);
                        //GLSL_ES_Fragment
                        ReadVividString(br);
                        //GLSL_Vertex
                        ReadVividString(br);
                        //GLSL_Fragment
                        ReadVividString(br);
                        //HLSL9_Vertex
                        ReadVividString(br);
                        //HLSL9_Fragment
                        ReadVividString(br);
                        //Position
                        br.ReadUInt32();
                        //Position
                        br.ReadUInt32();
                        uint attributesCount = br.ReadUInt32();
                        for(int i2 = 0; i2 < attributesCount; i2++)
                        {
                            //Name
                            ReadVividString(br);
                        }
                        br.BaseStream.Position = shaderPos;
                    }
                    break;
                case "FONT":
                    uint fontCount = br.ReadUInt32();
                    long fontPos;
                    for (int i = 0; i < fontCount; i++)
                    {
                        uint fontPointer = br.ReadUInt32();
                        fontPos = br.BaseStream.Position;
                        br.BaseStream.Position = fontPointer;
                        //Name
                        ReadVividString(br);
                        //DisplayName
                        ReadVividString(br);
                        //Does write Texture object pointer, may need to edit this later
                        br.BaseStream.Position = fontPos;
                    }
                    break;
                case "TMLN":
                    uint timelineCount = br.ReadUInt32();
                    long timelinePos;
                    for(int i = 0; i < timelineCount; i++)
                    {
                        uint timelinePointer = br.ReadUInt32();
                        timelinePos = br.BaseStream.Position;
                        br.BaseStream.Position = timelinePointer;
                        //Name
                        ReadVividString(br);
                        int momentCount = br.ReadInt32();
                        for (int i2 = 0; i2 < momentCount; i2++)
                        {
                            //Timepoints
                            br.ReadUInt32();
                            //unnesseceary pointers
                            br.ReadInt32();
                        }
                        for(int i3 = 0; i3 < momentCount; i3++)
                        {
                            uint actionCount = br.ReadUInt32();
                            long actionPos;
                            for(i3 = 0; i3 < actionCount; i3++)
                            {
                                uint actionPointer = br.ReadUInt32();
                                actionPos = br.BaseStream.Position;
                                br.BaseStream.Position = actionPointer;
                                //LibID
                                br.ReadUInt32();
                                //ID
                                br.ReadUInt32();
                                //Kind
                                br.ReadUInt32();
                                //UseRelative
                                br.ReadBoolean();
                                //IsQuestion
                                br.ReadBoolean();
                                //UseApplyTo
                                br.ReadBoolean();
                                //ExeType
                                br.ReadUInt32();
                                //ActionName
                                ReadVividString(br);
                                br.BaseStream.Position = actionPos;
                            }
                            
                        }
                        //Make the loops actually loop back to the original pointer
                        br.BaseStream.Position = timelinePos;
                    }
                    break;
                case "OBJT":
                    uint gameObjectCount = br.ReadUInt32();
                    long gameObjectPos;
                    for(int i = 0; i < gameObjectCount; i++)
                    {
                        uint gameObjectPointer = br.ReadUInt32();
                        gameObjectPos = br.BaseStream.Position;
                        br.BaseStream.Position = gameObjectPointer;
                        //Name
                        ReadVividString(br);
                        //_sprite
                        br.ReadUInt32();
                        //Visible
                        br.ReadUInt32();
                        //2022.5
                        //Managed
                        br.ReadUInt32();
                        //Done
                        //Solid
                        br.ReadUInt32();
                        //Depth
                        br.ReadInt32();
                        //Persistent
                        br.ReadUInt32();
                        //parent
                        br.ReadInt32();
                        //_textureMaskId
                        br.ReadUInt32();
                        //UsesPhysics
                        br.ReadUInt32();
                        //IsSensor
                        br.ReadUInt32();
                        //CollisionShape
                        br.ReadUInt32();
                        //Density
                        br.ReadSingle();
                        //Restitution
                        br.ReadSingle();
                        //Group
                        br.ReadUInt32();
                        //LinearDamping
                        br.ReadSingle();
                        //AngularDamping
                        br.ReadSingle();
                        int physicsShapeVertexCount = br.ReadInt32();
                        //Friction
                        br.ReadSingle();
                        //Awake
                        br.ReadUInt32();
                        //Kinematic
                        br.ReadUInt32();
                        for(int i2 = 0; i2 < physicsShapeVertexCount; i2++)
                        {
                            //X
                            br.ReadSingle();
                            //Y
                            br.ReadSingle();
                        }
                        uint countofAmountOfPointerLists = br.ReadUInt32();
                        long pointerPointerListsPos;
                        for (int i3 = 0; i3 < countofAmountOfPointerLists; i3++)
                        {
                            uint pointerPointerListPointer = br.ReadUInt32();
                            pointerPointerListsPos = br.BaseStream.Position;
                            br.BaseStream.Position = pointerPointerListPointer;
                            uint countOfPointers = br.ReadUInt32();
                            long pointerPos;
                            for(int i4 = 0; i4 < countOfPointers; i4++)
                            {
                                uint eventPointer = br.ReadUInt32();
                                pointerPos = br.BaseStream.Position;
                                br.BaseStream.Position = eventPointer;
                                //EventSubtype
                                br.ReadUInt32();
                                uint countOfEventActions = br.ReadUInt32();
                                long actionPos;
                                for(int i5 = 0; i5 < countOfEventActions; i5++)
                                {
                                    uint actionPointer = br.ReadUInt32();
                                    actionPos = br.BaseStream.Position;
                                    br.BaseStream.Position = actionPointer;
                                    br.BaseStream.Position = actionPos;
                                }
                                br.BaseStream.Position = pointerPos;

                            }
                            br.BaseStream.Position = pointerPointerListsPos;

                        }
                        //INCOMPLETE: Didnt handle pointer list of events
                        //still leaving this incomplete because idk if its required
                        br.BaseStream.Position = gameObjectPos;
                    }
                    break;
                case "ROOM":
                    uint roomCount = br.ReadUInt32();
                    long roomPos;
                    for(int i = 0; i < roomCount; i++)
                    {
                        uint gameObjectPointer = br.ReadUInt32();
                        roomPos = br.BaseStream.Position;
                        br.BaseStream.Position = gameObjectPointer;
                        //Name
                        ReadVividString(br);
                        //Caption
                        ReadVividString(br);
                        //Width
                        br.ReadUInt32();
                        //Height
                        br.ReadUInt32();
                        //Speed
                        br.ReadUInt32();
                        //Persistent
                        br.ReadUInt32();
                        //BackgroundColor (did not actually do calculation)
                        br.ReadUInt32();
                        //DrawBackgroundColor
                        br.ReadUInt32();
                        //_creationCodeId
                        br.ReadInt32();
                        //Flags
                        br.ReadUInt32();
                        //Backgrounds
                        br.ReadUInt32();
                        //Views
                        br.ReadUInt32();
                        //GameObjects
                        br.ReadUInt32();
                        //Tiles
                        br.ReadUInt32();
                        //World
                        br.ReadUInt32();
                        //Top
                        br.ReadUInt32();
                        //Left
                        br.ReadUInt32();
                        //Right
                        br.ReadUInt32();
                        //Bottom
                        br.ReadUInt32();
                        //GravityX
                        br.ReadSingle();
                        //GravityY
                        br.ReadSingle();
                        //MetersPerPixel
                        br.ReadSingle();
                        long layerListPos;
                        uint layerListPointer = br.ReadUInt32();
                        layerListPos = br.BaseStream.Position;
                        br.BaseStream.Position = layerListPointer;
                        uint layerCount = br.ReadUInt32();
                        long layerPos;
                        for(int i2 = 0; i2 < layerCount; i2++)
                        {
                            uint layerPointer = br.ReadUInt32();
                            layerPos = br.BaseStream.Position;
                            br.BaseStream.Position = layerPointer;
                            //LayerName
                            ReadVividString(br);
                            //LayerId
                            br.ReadUInt32();
                            //LayerType
                            uint layerType = br.ReadUInt32();
                            //LayerDepth
                            br.ReadInt32();
                            //XOffset
                            br.ReadSingle();
                            //YOffset
                            br.ReadSingle();
                            //HSpeed
                            br.ReadSingle();
                            //VSpeed
                            br.ReadSingle();
                            //IsVisible
                            br.ReadUInt32();
                            //2022.1
                            //EffectEnabled
                            br.ReadUInt32();
                            //EffectType
                            ReadVividString(br);
                            uint count = br.ReadUInt32();
                            for(int i3 = 0; i3 < count; i3++)
                            {
                                //Kind
                                br.ReadInt32();
                                //Name
                                ReadVividString(br);
                                //Value
                                ReadVividString(br);
                            }
                            if(layerType == 3)
                            {
                                //Assets layer
                                //LegacyTiles
                                br.ReadUInt32();
                                uint spritePointerList = br.ReadUInt32();
                                //2.3
                                uint sequencePointerList = br.ReadUInt32();
                                //>2.3.2
                                //NineSlices
                                //Done
                                //NonLTSVersionAtleast 2023.2
                                uint particleSystemsPointerList = br.ReadUInt32();
                                //2024.6
                                //TextItems
                                //Done
                                br.BaseStream.Position = spritePointerList;
                                uint spritePointerCount = br.ReadUInt32();
                                long spritePosition;
                                for(int i3 = 0; i3 < spritePointerCount; i3++)
                                {
                                    uint spritePointer = br.ReadUInt32();
                                    spritePosition = br.BaseStream.Position;
                                    br.BaseStream.Position = spritePointer;
                                    ReadVividString(br);
                                    br.BaseStream.Position = spritePosition;
                                }
                                br.BaseStream.Position = sequencePointerList;
                                uint sequencePointerCount = br.ReadUInt32();
                                long sequencePos;
                                for(int i3 = 0; i3 < sequencePointerCount; i3++)
                                {
                                    uint sequencePointer = br.ReadUInt32();
                                    sequencePos = br.BaseStream.Position;
                                    br.BaseStream.Position = sequencePointer;
                                    ReadVividString(br);
                                    br.BaseStream.Position = sequencePos;
                                }
                                uint particleSystemsPointerCount = br.ReadUInt32();
                                long particleSystemsPos;
                                for(int i3 = 0; i3 <  particleSystemsPointerCount; i3++)
                                {
                                    uint particleSystemsPointer = br.ReadUInt32();
                                    particleSystemsPos = br.BaseStream.Position;
                                    br.BaseStream.Position = particleSystemsPointer;
                                    ReadVividString(br);
                                    br.BaseStream.Position = particleSystemsPos;
                                }
                            }
                            br.BaseStream.Position = layerPos;
                        }
                        //Check if the references to gameobjects are required to be re-read.
                        br.BaseStream.Position = roomPos;
                    }
                    break;
                case "TPAG":
                    //Pretty sure no strings
                    break;
                case "CODE":
                    //Praying I don't have to touch it. Name (PRETTY SURE I DO PAIN)
                    uint codeCount = br.ReadUInt32();
                    long codePos;
                    for (int i = 0; i < codeCount; i++)
                    {
                        uint codePointer = br.ReadUInt32();
                        codePos = br.BaseStream.Position;
                        br.BaseStream.Position = codePointer;
                        //Name
                        ReadVividString(br);
                        //Length
                        uint length = br.ReadUInt32();
                        //LocalsCount
                        br.ReadUInt16();
                        //ArgumentsCount
                        br.ReadUInt16();
                        int BytecodeRelativeAddress = br.ReadInt32();
                        uint _bytecodeAbsoluteAddress = (uint)(br.BaseStream.Position - 4 + BytecodeRelativeAddress);
                        long here = br.BaseStream.Position;
                        br.BaseStream.Position = here;
                        while (br.BaseStream.Position < _bytecodeAbsoluteAddress + length)
                        {
                            long instructionStartAddress = br.BaseStream.Position;
                            br.BaseStream.Position += 3;
                            byte kind = br.ReadByte();
                            Opcode Kind = (Opcode)kind;
                            if(Kind == Opcode.Push || Kind == Opcode.PushLoc || Kind == Opcode.PushGlb || Kind == Opcode.PushBltn || Kind == Opcode.PushI)
                            {
                                short val = br.ReadInt16();
                                DataType Type1 = (DataType)br.ReadByte();
                                br.BaseStream.Position++;
                                if(Type1 == DataType.String)
                                {

                                }
                            }
                        }
                        br.BaseStream.Position = codePos;
                    }
                    break;
                case "VARI":
                    long startPosition = br.BaseStream.Position;
                    uint variableCount = br.ReadUInt32();
                    uint variableCount2 = br.ReadUInt32();
                    //MaxLocalVarCount
                    br.ReadUInt32();
                    long variablePos;
                    uint varLength = 20;
                    while (br.BaseStream.Position + varLength <= startPosition + jump)
                    {
                        //Name
                        ReadVividString(br);
                        //BytecodeVersion >= 15
                        //InstanceType
                        br.ReadInt32();
                        //VarID
                        br.ReadInt32();
                        uint occurences = br.ReadUInt32();
                        if (occurences > 0)
                        {
                            //FirstAddress
                            br.ReadUInt32();
                        }
                        else
                        {
                            if (br.ReadInt32() != -1)
                            {
                                //error but idrc
                            }
                        }
                    }
                    break;
                case "FUNC":
                    uint functionCount = br.ReadUInt32();
                    for (int i = 0; i < functionCount; i++)
                    {
                        //Name
                        ReadVividString(br);
                        //Occurences
                        uint occurences = br.ReadUInt32();
                        //InstructionPointer
                        br.ReadUInt32();
                    }
                    uint localCount = br.ReadUInt32();
                    for (int i = 0; i < localCount; i++)
                    {
                        uint listLocalCount = br.ReadUInt32();
                        //Name
                        ReadVividString(br);
                        for(uint i2 = 0; i2 < listLocalCount; i2++)
                        {
                            //Index
                            br.ReadUInt32();
                            //Name
                            ReadVividString(br);
                        }

                    }
                    break;
                case "STRG":
                    //Parsed fully already, ignore.
                    break;
                case "EMBI":
                    //Version
                    br.ReadUInt32();
                    uint imageCount = br.ReadUInt32();
                    for(int i = 0; i < imageCount; i++)
                    {
                        //Name
                        ReadVividString(br);
                        //TextureEntry, dont know how to handle
                        br.ReadUInt32();
                    }
                    break;
                case "TXTR":
                    //No strings.
                    break;
                case "TGIN":
                    //Version
                    br.ReadUInt32();
                    uint groupInfoCount = br.ReadUInt32();
                    long tgroupPos;
                    for(int i = 0; i < groupInfoCount; i++)
                    {
                        uint groupPointer = br.ReadUInt32();
                        tgroupPos = br.BaseStream.Position;
                        br.BaseStream.Position = groupPointer;
                        //Name
                        ReadVividString(br);
                        //2022.9
                        //Directory
                        ReadVividString(br);
                        //Extension
                        ReadVividString(br);
                        br.BaseStream.Position = tgroupPos;
                    }
                    break;
                case "AUDO":
                    //No strings.
                    break;
                case "TAGS":
                    uint tagStringCount = br.ReadUInt32();
                    for(int i = 0; i < tagStringCount; i++)
                    {
                        ReadVividString(br);
                    }
                    //Research later
                    break;
                case "ACRV":
                    //Version
                    br.ReadUInt32();
                    uint curveCount = br.ReadUInt32();
                    long curvePos;
                    for(int i = 0; i < curveCount; i++)
                    {
                        uint curvePointer = br.ReadUInt32();
                        curvePos = br.BaseStream.Position;
                        br.BaseStream.Position = curvePointer;
                        //Name
                        ReadVividString(br);
                        //GraphType
                        br.ReadUInt32();
                        uint channelCount = br.ReadUInt32();
                        for(int i2 = 0; i2 < channelCount; i2++)
                        {
                            //Name
                            ReadVividString(br);
                            //Curve
                            br.ReadUInt32();
                            //Iterations
                            br.ReadUInt32();
                            uint pointCount = br.ReadUInt32();
                            for(int i3 = 0; i3 < pointCount; i3++)
                            {
                                //X
                                br.ReadSingle();
                                //Y
                                br.ReadSingle();
                                //Skipping
                                br.BaseStream.Position += 4;
                            }
                        }
                        br.BaseStream.Position = curvePos;
                    }
                    break;
                case "SEQN":
                    //skipping for now what the actual fuck
                    uint sequenceCount = br.ReadUInt32();
                    for(int i = 0; i < sequenceCount; i++)
                    {
                        //Name
                        ReadVividString(br);
                        //Playback
                        br.ReadUInt32();
                        //PlaybackSpeed
                        br.ReadSingle();
                        //PlaybackSpeedType 
                        br.ReadUInt32();
                        //Length
                        br.ReadSingle();
                        //OriginX
                        br.ReadInt32();
                        //OriginY
                        br.ReadInt32();
                        //Volume
                        br.ReadSingle();
                        uint keyFrameCount = br.ReadUInt32();
                        for(int i2 = 0; i2 < keyFrameCount; i2++)
                        {
                            //Key
                            br.ReadSingle();
                            //Length
                            br.ReadSingle();
                            //Stretch
                            br.ReadBoolean();
                            //Disabled
                            br.ReadBoolean();
                            int count = br.ReadInt32();
                            for(int i3 = 0; i3 < count; i3++)
                            {
                                int channel = br.ReadInt32();
                                uint stringCount = br.ReadUInt32();
                                for(int i4 = 0; i4 < stringCount; i4++)
                                {
                                    //BroadcastMessage
                                    ReadVividString(br);
                                }
                            }
                        }

                        //Kinda have to read the entire chunk to get to strings, come back later.
                    }
                    break;
                case "FEAT":
                    uint flagCount = br.ReadUInt32();
                    for(int i = 0; i < flagCount; i++)
                    {
                        //Flag
                        ReadVividString(br);
                    }
                    break;
                case "FEDS":
                    //Version
                    br.ReadUInt32();
                    uint filterEffectCount = br.ReadUInt32();
                    long filterPos;
                    for(int i = 0; i < filterEffectCount; i++)
                    {
                        uint filterPointer = br.ReadUInt32();
                        filterPos = br.BaseStream.Position;
                        br.BaseStream.Position = filterPointer;
                        //Name
                        ReadVividString(br);
                        //Value
                        ReadVividString(br);
                        br.BaseStream.Position = filterPos;
                    }
                    break;
                case "PSYS":
                    //Version
                    br.ReadUInt32();
                    uint systemCount = br.ReadUInt32();
                    long systemPos;
                    for (int i = 0; i < systemCount; i++)
                    {
                        uint systemPointer = br.ReadUInt32();
                        systemPos = br.BaseStream.Position;
                        br.BaseStream.Position = systemPointer;
                        //Name
                        ReadVividString(br);
                        //Read rest of chunk
                        //Pointer list, go back and check.
                        br.BaseStream.Position = systemPos;
                    }
                    break;
                case "PSEM":
                    //Version
                    br.ReadUInt32();
                    uint emitterCount = br.ReadUInt32();
                    long emitterPos;
                    for (int i = 0; i < emitterCount; i++)
                    {
                        uint emitterPointer = br.ReadUInt32();
                        emitterPos = br.BaseStream.Position;
                        br.BaseStream.Position = emitterPointer;
                        //Name
                        if (ReadVividString(br) == null)
                        {
                            break;
                        }
                        br.BaseStream.Position = emitterPos; 
                    }
                    break;
            }

        }
        public string ReadVividString(BinaryReader br)
        {
            long beforeReadingPointer = br.BaseStream.Position;
            uint pointer = br.ReadUInt32();
            if(pointer == 0)
            {
                return null;
            }
            long savedBeforeReadingPos = br.BaseStream.Position;
            br.BaseStream.Position = pointer - 4;
            uint length = br.ReadUInt32();
            string vividstasisstring = Encoding.UTF8.GetString(br.ReadBytes((int)length));
            readStringReferences.Add(new StringReference(vividstasisstring,beforeReadingPointer,pointer));
            br.BaseStream.Position = savedBeforeReadingPos;
            return vividstasisstring;
        }
        string filePath
        {
            get { return _filepath; }
            set
            {
                _filepath = value;
                load.Show();
                Thread t = new Thread(new ThreadStart(LoadFile));
                t.Start();
            }
        }
        private static string _filepath;
        public void DisplayError(string msg, Exception e = null)
        {
            if(e != null)
            {
                MessageBox.Show(msg + $" Exception: {e.Message}", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } else
            {
                MessageBox.Show(msg, "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            load.Invoke(new Action(() => { load.Hide(); }));
            return;
        }
        public static TreeNodeCollection SortEpisodes(TreeNodeCollection chapter)
        {
            int minparsedepisode = 0;
            for (int i = 0; i < chapter.Count - 1; i++)
            {
                int min_idx = i;
                for (int j = i + 1; j < chapter.Count; j++)
                {
                    int parsedepisode = 0;
                    try
                    {
                        parsedepisode = Int32.Parse(chapter[j].Text.Split(' ')[1]);
                    }
                    catch (Exception)
                    {
                        string removed = chapter[j].Text.Split(' ')[1].Remove(chapter[j].Text.Split(' ')[1].Length - 1, 1);
                        parsedepisode = Int32.Parse(removed);
                    }
                    try
                    {
                        minparsedepisode = Int32.Parse(chapter[min_idx].Text.Split(' ')[1]);
                    }
                    catch (Exception)
                    {
                        minparsedepisode = Int32.Parse(chapter[min_idx].Text.Split(' ')[1].Remove(chapter[j].Text.Split(' ').Length - 1, 1));
                    }
                    if (parsedepisode < minparsedepisode)
                    {
                        minparsedepisode = parsedepisode;
                        min_idx = j;
                    }
                }
                TreeNode temp = chapter[min_idx];
                chapter[min_idx] = chapter[i];
                chapter[i] = temp;
            }
            return chapter;
        }
        public Form1()
        {
            InitializeComponent();
            Text = Text = $@"vivid/stasis Text Editor v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion} - No game loaded";
            if(Updater.CheckForUpdates())
            {
                //Update available
                if(MessageBox.Show("An update is available for vivid/stasis text editor. Would you like to download it?", "vivid/stasis Text Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    //Download the update
                    Updater.DownloadUpdate(load);
                }
            }
        }
        public void EditLoadingMessage(string msg)
        {
             load.EditLoadingMessage(msg);
        }

        public void EditLoadingTitle(string msg)
        {
            load.EditLoadingTitle(msg);
        }
        public static string FormatEpisode(string nonFormattedEpisode)
        {
            string formattedEpisode;
            if (nonFormattedEpisode.Length >= 3)
            {
                 formattedEpisode = nonFormattedEpisode.Substring(1, 2);
            }
            else
            {
                 formattedEpisode = nonFormattedEpisode.Substring(1, 1);
            }
            if (formattedEpisode.Substring(1).Length > 1)
            {
                //Letter at the end
                formattedEpisode += "." + nonFormattedEpisode[nonFormattedEpisode.Length-1] % 32;
            }
            return formattedEpisode;
        }
        public void AddNode(int chapternum, string nonFormattedEpisode, int i)
        {
            string episode = "Episode " + FormatEpisode(nonFormattedEpisode);
            TreeNode episodeNode = new TreeNode();
            TreeNodeCollection storyNodes = treeView1.Nodes[0].Nodes[chapternum - 1].Nodes;
            bool containsEpisode = false;
            foreach (TreeNode storyNode in storyNodes)
            {
                if(storyNode.Text == episode)
                {
                    containsEpisode = true;
                    episodeNode = storyNode;
                }
            }    
            if (containsEpisode)
            {
                episodeNode.Nodes.Add(vividStasisStrings[i]);
            } else
            {
                storyNodes.Add(episode).Nodes.Add(vividStasisStrings[i]);
            }
            
        }
        public void LoadFile()
        {
            vividStasisStrings.Clear();
            stringPointers.Clear();
            storyStrings.Clear();
            gameStrings.Clear();
            unknownStrings.Clear();
            if(Path.GetFileName(filePath) != "data.win")
            {
                //Joke for RGB, the program does not really care about this
                // rgb here - the below is funny, keep it if you want
                MessageBox.Show("NAME WARNI?!?! (YEP, RENAME YOUR FILE RGB, GET FUCKED)", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (File.Exists(filePath))
            {
                FileStream s = null;
                byte[] data = new byte[4];
                try
                {
                   s = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
                } catch(IOException)
                {
                    DisplayError("Error attempting to read the file, check your permissions and if any other program is using it.");
                    return;
                }
                s.Read(data, 0, 4);
                //Verify data.win
                if (data[0] == 70 && data[1] == 79 && data[2] == 82 && data[3] == 77)
                {
                    using (BinaryReader br = new BinaryReader(s))
                    {
                        data = new byte[4];
                        s.Position += 4;
                        string chunkName = new string(br.ReadChars(4));
                        uint jump = br.ReadUInt32();
                        long savePos = br.BaseStream.Position;
                        //GEN8 Chunk, Read
                        EditLoadingMessage("Reading chunk GEN8");
                        while (chunkName != "STRG")
                        {
                            //Unserialize so we get all the string references
                            //Ignore, and jump
                            try
                            {
                                if(chunkName != "GEN8")
                                {
                                    jump = br.ReadUInt32();
                                    savePos = br.BaseStream.Position;
                                }
                                Unserialize(chunkName, br, jump, savePos);
                                s.Position = savePos;
                                s.Position += jump;
                            } catch(Exception e)
                            {
                                DisplayError("An error occured while attempting to find the STRG chunk. (Make sure you are passing in a valid data.win file!)", e);
                                return;
                            }
                            chunkName = new string(br.ReadChars(4));
                        }
                        load.Invoke(new Action(() => { load.EditLoadingMessage("Searching for amount of strings..."); }));
                        s.Position += 4;
                        uint amountOfStrings = br.ReadUInt32();
                        label5.Invoke(new Action(() => { label5.Text = $"Strings: {amountOfStrings}"; }));
                        uint stringPointer = br.ReadUInt32();
                        long pos = s.Position;
                        s.Position = stringPointer;
                        int stringLength = br.ReadInt32();
                        string str = new string(br.ReadChars(stringLength));
                        vividStasisStrings.Add(str);
                        stringPointers.Add(str, stringPointer);
                        load.Invoke(new Action(() => { load.EditLoadingMessage($"Adding strings into database..."); }));
                        for (int i = 1; i < amountOfStrings; i++)
                        {
                            if(i != 1)
                            {
                                pos += 4;
                            }
                            s.Position = pos;
                            stringPointer = br.ReadUInt32();
                            if(stringPointer == 4613856)
                            {
                                Debug.Write("what the fuck");
                            }
                            s.Position = stringPointer;
                            stringLength = br.ReadInt32();
                            try
                            {
                                str = Encoding.UTF8.GetString(br.ReadBytes(stringLength));
                            } catch(Exception e)
                            {
                                DisplayError("An error occured while attempting to add strings to the database. File may be corrupt. (Make sure you are passing in a valid data.win file!)", e);
                            }
                            if (str.Contains("keysmashes"))
                            {
                                Debug.Write("what the fuck");
                            }
                            vividStasisStrings.Add(str);
                            stringPointers.Add(str, stringPointer);
                            //load.Invoke(new Action(() => { load.EditLoadingMessage($"Adding strings into database ({i}/{amountOfStrings}...)"); }));
                        }
                        //MessageBox.Show("DEBUG: Successfully loaded all strings");
                        //The first pointer to the string
                    }
                    s.Close();
                    //Process the strings
                    load.Invoke(new Action(() => { load.EditLoadingMessage("Processing strings..."); }));
                    for(int i = 0; i < vividStasisStrings.Count; i++)
                    {
                        if ((!vividStasisStrings[i].Contains("ss_c") && !vividStasisStrings[i].Contains("ss_a") && i > 0) && (vividStasisStrings[i-1].Contains("ss_c") || vividStasisStrings[i-1].Contains("ss_a")))
                        {
                            //Chapter flag
                            //TODO: Add ASTELLION and other SONG dialogue
                            if (vividStasisStrings[i].Contains("gml_Script_"))
                            {
                                unknownStrings.Add(vividStasisStrings[i]);
                                continue;
                            }
                            string[] splitString = vividStasisStrings[i - 1].Split("_".ToCharArray());
                            //ss is always right before the chapter and episode number
                            int indexOfss = Array.IndexOf(splitString, "ss");
                            if(indexOfss == -1)
                            {
                                foreach (string part in splitString)
                                {
                                    if (part.Contains("ss"))
                                    {
                                        //No dialogue gets added unless we seperate using @.
                                        string[] testArray = part.Split('@');
                                        if (Array.IndexOf(testArray, "ss") == -1)
                                        {
                                            unknownStrings.Add(vividStasisStrings[i]);
                                            continue;
                                        }
                                        else
                                        {
                                            indexOfss = Array.IndexOf(splitString, part);
                                            break;
                                        }
                                    }
                                }
                            }
                            //nonformattedChapter = c1
                            //nonFormattedEpisode = e1
                            string nonFormattedChapter = splitString[indexOfss + 1];
                            string nonFormattedEpisode = splitString[indexOfss + 2];
                            int episodenum = 0;
                            int chapternum = 0;
                            try
                            {
                                if(nonFormattedEpisode.Length >= 3)
                                {
                                    try
                                    {
                                        episodenum = Int32.Parse(nonFormattedEpisode.Substring(1, 2));
                                    } catch(Exception)
                                    {
                                        //If there is a letter on the end of the episode.
                                        episodenum = Int32.Parse(nonFormattedEpisode.Substring(1, 1));
                                    }  
                                    
                                } else
                                {
                                    episodenum = Int32.Parse(nonFormattedEpisode.Substring(1, 1));
                                }
                                chapternum = Int32.Parse(nonFormattedChapter.Substring(1));
                            } catch(Exception e)
                            {
                                //Something trying to sneak into story, ignore.
                                unknownStrings.Add(vividStasisStrings[i]);
                                continue;
                            }
                            //Chapter and episode adding into the treeview.
                            if (chapternum == 1 && episodenum <= 7)
                            {
                                //Ok for chapter 1
                                AddNode(1, nonFormattedEpisode, i);
                                continue;
                            } else if(chapternum == 1 &&  episodenum > 7)
                            {
                                //Lie, could be in any chapter
                                int chapterNumber = 2;
                                if (episodenum <= 15)
                                {
                                    //Chapter 2
                                    chapterNumber = 2;
                                }
                                else if (episodenum <= 22)
                                {
                                    //Chapter 3
                                    chapterNumber = 3;
                                }
                                else
                                {
                                    //Chapter 4
                                    chapterNumber = 4;
                                }
                                AddNode(chapterNumber, nonFormattedEpisode, i);
                                continue;
                            }
                            else
                            {
                                //Identifying itsself as chapter 2
                                if(episodenum <= 15)
                                {
                                    //Chapter 2
                                    AddNode(2, nonFormattedEpisode, i);
                                    continue;
                                }
                            }
                            storyStrings.Add(vividStasisStrings[i]);
                            //vividStasisStrings[i-1].Split("ss_c")
                        } else if (vividStasisStrings[i].Contains("This episode")) 
                        {
                            //TODO: Add more game strings other than just content warnings
                            gameStrings.Add(vividStasisStrings[i]);
                        } else
                        {
                            unknownStrings.Add(vividStasisStrings[i]);
                        }


                    }
                    label1.Invoke(new Action(() =>
                    {
                        label1.Visible = false;
                        label2.Visible = false;
                        label3.Visible = false;
                    }));
                    load.Invoke(new Action(() => { load.EditLoadingMessage($"Sorting episodes..."); }));
                    TreeNodeCollection chapter1 = treeView1.Nodes[0].Nodes[0].Nodes;
                    TreeNodeCollection chapter2 = treeView1.Nodes[0].Nodes[1].Nodes;
                    TreeNodeCollection chapter3 = treeView1.Nodes[0].Nodes[2].Nodes;
                    TreeNodeCollection chapter4 = treeView1.Nodes[0].Nodes[3].Nodes;
                    TreeNodeCollection chapter5 = treeView1.Nodes[0].Nodes[4].Nodes;
                    int minparsedepisode = 0;
                    chapter1 = SortEpisodes(chapter1);
                    chapter2 = SortEpisodes(chapter2);
                    chapter3 = SortEpisodes(chapter3);
                    chapter4 = SortEpisodes(chapter4);
                    chapter5 = SortEpisodes(chapter5);
                    foreach (string game in gameStrings)
                    {
                        treeView1.Nodes[1].Nodes.Add(new TreeNode(game));
                    }
                    foreach(string unknown in unknownStrings)
                    {
                        treeView1.Nodes[2].Nodes.Add(new TreeNode(unknown));
                    }
                    treeView1.Invoke(new Action(() =>
                    {
                        //Showing the editor UI.
                        treeView1.Visible = true;
                        label4.Visible = true;
                        richTextBox1.Visible = true;
                        button1.Visible = true;
                        label5.Visible = true;
                        load.Hide();
                    }));
                    this.Invoke(new Action(() => { Text = $@"vivid/stasis Text Editor v{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion} - ""{displayName}"" (GM unknown) [{filePath}]"; }));
                    if(displayName != "vivid/stasis")
                    {
                        MessageBox.Show("This game is not vivid/stasis. Note that this program is only meant to be used on vivid/stasis. Bugs may occur.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    MessageBox.Show("This program is shitcoded by MichaelEpicA, and bugs may occur. Please report them to the official github. (https://github.com/MichaelEpicA/vividstasis-Text-Editor)", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("This is not a valid data.win, the provided format does not match with the supported format.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    load.Invoke(new Action(() => { load.Hide(); }));
                }

            }
            else
            {
                MessageBox.Show("Catastrophic error. (File not found)", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                load.Invoke(new Action(() => { load.Hide(); }));
            }
        }
        public void SaveFile()
        {
            FileStream s = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);
            FileStream sbak = new FileStream(Path.ChangeExtension(filePath, ".vste"), FileMode.Create, FileAccess.ReadWrite);
            load.Invoke(new Action(() => { load.Show(); }));
            EditLoadingTitle("Writing, please wait...");
            EditLoadingMessage("Writing data.win back to vivid/stasis...");
            using (BinaryReader sr = new BinaryReader(s))
            {
                using (BinaryWriter sw = new BinaryWriter(sbak))
                {
                    //Write FORM indicator
                    sw.Write(sr.ReadChars(4));
                    sw.Write(sr.ReadUInt32());
                    char[] chunkName = new char[4];
                    long unserializepos;
                    int jump = 0;
                    long offsetwritepos = 4;
                    int srpos = 0;
                    long pos2 = 0;
                    long strgEndPos = 0;
                    while (new string(chunkName) != "STRG")
                    {
                        chunkName = sr.ReadChars(4);
                        EditLoadingMessage($"Writing chunk {new string(chunkName)}");
                        sw.Write(chunkName);
                        srpos = (int)sr.BaseStream.Position;
                        offsetwritepos = sw.BaseStream.Position;
                        jump = sr.ReadInt32();
                        sw.Write(jump);
                        if (new string(chunkName) != "STRG")
                        {
                            sw.Write(sr.ReadBytes(jump));
                        } else
                        {
                            strgEndPos = sr.BaseStream.Position + jump;
                        }

                    }
                    //Writing amount of strings
                    uint amountOfStrings = sr.ReadUInt32();
                    long pos = 0;
                    sw.Write(amountOfStrings);
                    pos = sw.BaseStream.Position;
                    for (int i = 0; i < amountOfStrings; i++)
                    {
                        //Writing string pointers
                        sw.Write(sr.ReadUInt32());
                    }
                    Dictionary<string, long> modifiedStringPointers = new Dictionary<string, long>();
                    foreach (string st in vividStasisStrings)
                    {
                        //Alignment
                        while (sw.BaseStream.Position % 4 != 0)
                        {
                            sw.Write((byte)0);
                        }
                        //TODO: Do the thing GameMaker does where its only on one line (Might be required)\
                        //The pattern is this: Add null bytes until there are only 4 bytes from reading the int32 right into the sttring
                        modifiedStringPointers.Add(st, sw.BaseStream.Position);
                        byte[] stringBytes = Encoding.UTF8.GetBytes(st);
                        sw.BaseStream.Write(BitConverter.GetBytes(stringBytes.Length), 0, BitConverter.GetBytes(stringBytes.Length).Length);
                        sw.BaseStream.Write(stringBytes, 0, stringBytes.Length);
                        if (!st.Contains(Encoding.UTF8.GetString(new[] { (byte)0 })))
                        {
                            sw.Write((byte)0);
                        }
                    }
                    pos2 = sw.BaseStream.Position;
                    sw.BaseStream.Position = pos;
                    //Chunk alignment
                    foreach (KeyValuePair<string, long> kvp in modifiedStringPointers)
                    {
                        sw.BaseStream.Write(BitConverter.GetBytes((int)kvp.Value), 0, BitConverter.GetBytes((int)kvp.Value).Length);
                    }
                    sw.BaseStream.Position = pos2;
                    while (sw.BaseStream.Position % 0x80 != 0)
                    {
                        sw.Write((byte)0);
                    }
                    pos2 = sw.BaseStream.Position;
                    long difference = sw.BaseStream.Position - strgEndPos;
                    chunkName = new char[] { ' ' };
                    sr.BaseStream.Position = srpos;
                    sr.BaseStream.Position += jump;
                    sr.BaseStream.Position += 4;
                    bool firstRun = true;
                    while (new string(chunkName) != "STRG") 
                    {
                        int off = (int)pos2 - (int)offsetwritepos;
                        if (!firstRun)
                        {
                            pos2 = sw.BaseStream.Position;
                            off = (int)pos2 - (int)offsetwritepos;
                            off -= 4;
                        } else
                        {
                            firstRun = false;
                            off -= 4;
                        }
                        sw.BaseStream.Position = offsetwritepos;
                        sw.Write(BitConverter.GetBytes(off), 0, BitConverter.GetBytes(off).Length);
                        sw.BaseStream.Position = pos2;
                        chunkName = sr.ReadChars(4);
                        EditLoadingMessage($"Writing chunk {new string(chunkName)}");
                        sw.Write(chunkName);
                        offsetwritepos = (int)sw.BaseStream.Position;
                        try
                        {
                            jump = sr.ReadInt32();
                            unserializepos = sr.BaseStream.Position;
                            Unserialize(new string(chunkName), sr, (uint)jump, 0, true);
                            sr.BaseStream.Position = unserializepos;
                        } catch(Exception e)
                        {
                            break;
                        }
                        if (new string(chunkName) == "AUDO")
                        {
                            // TODO:
                            // + Write chunk name
                            // + Write chunk size
                            // + Write num pointers
                            // - Rewrite pointers + difference
                            // - Write data
                            //Writing everything from chunkname raw (bad)

                            long startPos = sr.BaseStream.Position;
                            uint chunkSize = (uint)jump;
                            uint numPointers = sr.ReadUInt32();
                            // Write chunk size
                            long chunkSizePtr = sw.BaseStream.Position;
                            sw.Write(chunkSize);
                            // Write # pointers
                            sw.Write(numPointers);
                            // Fix pointers
                            for (uint i = 0; i < numPointers; i++) {
                                uint pointer = sr.ReadUInt32();
                                sw.Write((uint)(pointer + difference));
                            }
                            // Write audio data
                            sw.Write(sr.ReadBytes( (int)(chunkSize - (sr.BaseStream.Position - startPos))) );
                            // Alignment
                            long retPtr = sw.BaseStream.Position;
                            sw.BaseStream.Position = chunkSizePtr;
                            sw.Write((uint)(retPtr - (sw.BaseStream.Position+4)));
                            sw.BaseStream.Position = retPtr;
                            break;
                        } else if (new string(chunkName) == "TXTR")
                        {
                            Dictionary<long,long> texturePointerPositions = new Dictionary<long, long>();
                            long startPos = sr.BaseStream.Position;
                            uint chunkSize = (uint)jump;
                            uint numPointers = sr.ReadUInt32();
                            // Write chunk size
                            long chunkSizePtr = sw.BaseStream.Position;
                            sw.Write(chunkSize);
                            // Write # pointers
                            sw.Write(numPointers);
                            long txtrEndPos = startPos + chunkSize + difference;
                            // Fix pointers
                            long txtrPosition;
                            long txtrPositionWriter;
                            for (uint i = 0; i < numPointers; i++)
                            {
                                uint pointer = sr.ReadUInt32();
                                txtrPosition = sr.BaseStream.Position;
                                sr.BaseStream.Position = pointer;
                                uint finalPointer = (uint)(pointer + difference);
                                sw.Write(finalPointer);
                                txtrPositionWriter = sw.BaseStream.Position;
                                sw.BaseStream.Position = finalPointer;
                                texturePointerPositions.Add(sw.BaseStream.Position, sr.BaseStream.Position);
                                sr.BaseStream.Position = txtrPosition;
                                sw.BaseStream.Position = txtrPositionWriter;
                            }
                            // Write texture data
                            long offsetInChunk = (sr.BaseStream.Position - startPos);
                            long longNumBytesToRead = (chunkSize - offsetInChunk);
                            int numBytesToRead = (int)longNumBytesToRead;
                            sw.Write(sr.ReadBytes(numBytesToRead));
                            long writePosition = sw.BaseStream.Position;
                            long readPosition = sr.BaseStream.Position;
                            foreach(KeyValuePair<long,long> positions in texturePointerPositions)
                            {
                                sw.BaseStream.Position = positions.Key;
                                sr.BaseStream.Position = positions.Value;
                                //Scaled
                                sw.Write(sr.ReadUInt32());
                                //2.0.6
                                //GeneratedMips
                                sw.Write(sr.ReadUInt32());
                                //2022.3
                                //_textureBlockSize
                                sw.Write(sr.ReadUInt32());
                                //2022.9
                                //TextureWidth
                                sw.Write(sr.ReadUInt32());
                                //TextureHeight
                                sw.Write(sr.ReadUInt32());
                                //IndexInGroup
                                sw.Write(sr.ReadUInt32());
                                //_textureData
                                uint _textureData = sr.ReadUInt32();
                                if (_textureData != 0)
                                {
                                    sw.Write(_textureData + difference);
                                }
                            }
                            sw.BaseStream.Position = writePosition;
                            sr.BaseStream.Position = readPosition;
                            // Alignment
                            long retPtr = sw.BaseStream.Position;
                            sw.BaseStream.Position = chunkSizePtr;
                            sw.Write((uint)(retPtr - (sw.BaseStream.Position + 4)));
                            sw.BaseStream.Position = retPtr;
                            long added = sw.BaseStream.Position - txtrEndPos;
                            difference += added;    
                        } else
                        {
                            sw.Write(jump);
                            
                            if (new string(chunkName) != "STRG")
                            {
                                sw.Write(sr.ReadBytes(jump));

                                // Align ig
                                while (sw.BaseStream.Position % 0x80 != 0)
                                {
                                    sw.Write((byte)0);
                                }
                            }
                            else
                            {
                                sr.ReadBytes(jump);
                            }
                        }

                    }

                    long endPos = sw.BaseStream.Position;
                    sw.BaseStream.Position = 4;
                    sw.Write((uint)(endPos - (sw.BaseStream.Position + 4)));
                    EditLoadingMessage("Writing string references...");
                    foreach(StringReference reference in readStringReferences)
                    {
                        uint newPointer = (uint)modifiedStringPointers[reference.ReferencedString] + 4;
                            //Changing position to where the pointer is stored;
                            sw.BaseStream.Position = reference.Position;
                            sw.BaseStream.Write(BitConverter.GetBytes(newPointer), 0, BitConverter.GetBytes(newPointer.ToString().Length).Length);
                            
                    }
                }
            }
            load.Invoke(new Action(() => { load.Hide(); }));   
        }
        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop, true);
            filePath = filePaths[0];

        }
        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
           if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            } else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(e.Action == TreeViewAction.Expand || e.Action == TreeViewAction.Collapse)
            {
                return;
                //Ignore, we don't care.
            } else
            {
                richTextBox1.Text = e.Node.Text;
                origString = e.Node.Text;
                selectedNode = e.Node;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(new ThreadStart(SaveFile));
            t.Start();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            load.Show();
            load.EditLoadingMessage("Searching for STRG chunk...");
            await Task.Delay(5000);
            load.EditLoadingMessage("Searching for amount of strings...");
            await Task.Delay(2000);
            string[] num = label5.Text.Split("Strings: ".ToCharArray());
            for (int i = 0; i < Int32.Parse(num[9]); i++)
            {
                load.EditLoadingMessage($"Adding strings into database ({i + 1}/{num[9]}...)");
                await Task.Delay(1);
            }
            load.EditLoadingMessage("Processing strings...");
            await Task.Delay(3000);
            load.EditLoadingMessage($"Sorting episodes...");
            await Task.Delay(2000);
            load.EditLoadingMessage($"Complete.");
            await Task.Delay(2000);
        }   

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void textBox1_Leave(object sender, EventArgs e)
        {
            if(origString != "" && origString != null)
            {
                int index = vividStasisStrings.IndexOf(origString); 
                vividStasisStrings[index] = richTextBox1.Text;
                selectedNode.Text = richTextBox1.Text;
                selectedNode.TreeView.Refresh();
            }
            
        }
    }
}
