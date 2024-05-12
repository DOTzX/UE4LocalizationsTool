using AssetParser.Object;
using Helper.MemoryList;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetParser
{
    public class IoPackage : IUasset
    {
        public int LegacyFileVersion { get; set; }
        public UEVersions EngineVersion { get; set; }
        public EPackageFlags PackageFlags { get; set; }
        public int TotalHeaderSize { get; set; }
        public int NameCount { get; set; }
        public int NameOffset { get; set; }
        public int ExportCount { get; set; }
        public int ExportOffset { get; set; }
        public int ImportCount { get; set; }
        public int ImportOffset { get; set; }
        public int ExportBundleEntriesOffset { get; set; }
        public List<string> NameMap { get; set; }
        public List<ImportsDirectory> ImportMap { get; set; }
        public List<ExportsDirectory> ExportMap { get; set; }
        public MemoryList UassetFile { get; set; }
        public bool IOFile { get; set; } = true;
        public bool IsNotUseUexp { get; set; }
        public bool UseFromStruct { get; set; } = true;
        public bool AutoVersion { get; set; }
        public bool UseMethod2 { get; set; }
        public bool UseWithLocRes { get; set; }


        public int Header_Size { get; set; }
        public int Name_Directory_Size { get; set; }
        public int Hash_Directory_offset { get; set; }
        public int Hash_Directory_Size { get; set; }
        public int Bundles_Offset { get; set; }
        public int GraphData_Offset { get; set; }
        public int GraphData_Size { get; set; }
        public int PathCount { get; set; } = 0;
        public bool PathModify { get; set; } = true;

        public IoPackage(string FilePath)
        {
            UassetFile = new MemoryList(FilePath);
            //Todo 

            IsNotUseUexp = true;
            UassetFile.MemoryListPosition = 0;
            ConsoleMode.Print("Reading Uasset Header...");
            Console.WriteLine(UassetFile.GetIntValue(false, 4));



            UassetFile.Seek(UassetFile.GetIntValue(false, 24), SeekOrigin.Begin);

            string path = UassetFile.GetStringUES();
            UassetFile.Seek(0, SeekOrigin.Begin);


            if (path.StartsWith("/"))
            {
                EngineVersion = UEVersions.VER_UE4_16; //?!
                UE4Header();
            }
            else
            {
                EngineVersion = UEVersions.VER_UE5_0; //?!
                UE5Header();
            }

            //ScarletNexus-> Game/L10N 
            if (NameMap.First().StartsWith("/Game/L10N/"))
            {
                UseMethod2 = true;
            }
        }

        private void UE4Header()
        {
            UassetFile.Skip(16 + 4);
            Header_Size = UassetFile.GetIntValue();
            NameOffset = UassetFile.GetIntValue();
            Name_Directory_Size = UassetFile.GetIntValue();
            Hash_Directory_offset = UassetFile.GetIntValue();
            Hash_Directory_Size = UassetFile.GetIntValue();
            ImportOffset = UassetFile.GetIntValue();
            ExportOffset = UassetFile.GetIntValue();
            Bundles_Offset = UassetFile.GetIntValue();
            GraphData_Offset = UassetFile.GetIntValue();
            GraphData_Size = UassetFile.GetIntValue();


            TotalHeaderSize = GraphData_Offset + GraphData_Size;
            NameCount = Hash_Directory_Size / 8;
            ExportCount = (Bundles_Offset - ExportOffset) / 72 /*Block Size*/;
            ImportCount = (ExportOffset - ImportOffset) / 8 /*Block Size*/;


            //seek to position
            UassetFile.Seek(NameOffset, SeekOrigin.Begin);
            //Get Names
            NameMap = new List<string>();
            for (int n = 0; n < NameCount; n++)
            {
                NameMap.Add(UassetFile.GetStringUES());
                if (NameMap[n].Contains(@"/") && PathModify)
                {
                    PathCount++;
                }
                else
                {
                    PathModify = false;
                }
            }

            //UassetFile.Seek(Hash_Directory_offset, SeekOrigin.Begin);

            //seek to position
            UassetFile.Seek(ExportOffset, SeekOrigin.Begin);
            //Get Exports
            ExportMap = new List<ExportsDirectory>();
            ExportReadOrEdit();
        }

        private void UE5Header()
        {
            //this for ue5_0 only
            bool bHasVersioningInfo = UassetFile.GetUIntValue() == 1;
            Header_Size = UassetFile.GetIntValue();
            UassetFile.Skip(8); //name
            UassetFile.Skip(4); //PackageFlags
            UassetFile.Skip(4); //CookedHeaderSize
            UassetFile.Skip(4); //ImportedPublicExportHashesOffset
            ImportOffset = UassetFile.GetIntValue();
            ExportOffset = UassetFile.GetIntValue();
            ExportBundleEntriesOffset = UassetFile.GetIntValue();
            UassetFile.Skip(4); //GraphDataOffset

            TotalHeaderSize = Header_Size;
            ExportCount = (ExportBundleEntriesOffset - ExportOffset) / 72;
            ImportCount = (ExportOffset - ImportOffset) / sizeof(long);


            if (bHasVersioningInfo)
            {
                throw new Exception("Not supported uasset!");
            }

            //----------------------
            //Get Names
            NameMap = new List<string>();
            NameCount = UassetFile.GetIntValue();
            int NamesBlockSize = UassetFile.GetIntValue();
            UassetFile.Skip(8); //hashVersion
            UassetFile.Skip(NameCount * sizeof(long));//hashes
            var NamesHeader = UassetFile.GetShorts(NameCount);

            foreach (var header in NamesHeader)
            {
                NameMap.Add(UassetFile.GetStringUES(header));
            }


            //Get Exports
            UassetFile.Seek(ExportOffset, SeekOrigin.Begin);
            ExportMap = new List<ExportsDirectory>();
            ExportReadOrEdit();
        }

        public void EditName(string NewStr, int Index)
        {
            return;
        }

        public void ExportReadOrEdit(bool Modify = false)
        {
            //seek to position
            UassetFile.Seek(ExportOffset, SeekOrigin.Begin);
            int NextExportPosition = TotalHeaderSize;

            for (int n = 0; n < ExportCount; n++)
            {
                int Start = UassetFile.GetPosition();
                ExportsDirectory ExportsDirectory = new ExportsDirectory();
                ExportsDirectory.SerialOffset = TotalHeaderSize;
                if (!Modify)
                {
                    UassetFile.Skip(8);
                    ExportsDirectory.SerialSize = (int)UassetFile.GetInt64Value();
                }
                else
                {
                    UassetFile.SetInt64Value(Header_Size + (NextExportPosition - TotalHeaderSize));
                    UassetFile.SetInt64Value(ExportMap[n].ExportData.Count);
                }
                ExportsDirectory.ObjectName = UassetFile.GetFName(NameMap);
                UassetFile.Skip(8);

                //Wrong way
                ulong Class = UassetFile.GetUInt64Value();//CityHash64 ?!

                switch (Class)
                {
                    case 0x71E24A29987BD1EDu:
                        if (!NameMap.Contains("DataTable"))
                        {
                            NameMap.Add("DataTable");
                        }
                        ExportsDirectory.ClassIndex = NameMap.IndexOf("DataTable");
                        break;
                    case 0x70289FB93F770603u:

                        if (!NameMap.Contains("StringTable"))
                        {
                            NameMap.Add("StringTable");
                        }
                        ExportsDirectory.ClassIndex = NameMap.IndexOf("StringTable");

                        break;
                    case 0x574F27AEC05072D0u:
                        if (!NameMap.Contains("Function"))
                        {
                            NameMap.Add("Function");
                        }
                        ExportsDirectory.ClassIndex = NameMap.IndexOf("Function");
                        break;
                    default:
                        {
                            if (!NameMap.Contains("StructProperty"))
                            {
                                NameMap.Add("StructProperty");
                            }
                            ExportsDirectory.ClassIndex = NameMap.IndexOf("StructProperty");
                            break;
                        }
                }


                if (!Modify)
                {
                    ExportsDirectory.ExportData = new List<byte>();
                    ExportsDirectory.ExportData.AddRange(UassetFile.GetBytes(ExportsDirectory.SerialSize, false, NextExportPosition));
                    ExportMap.Add(ExportsDirectory);
                }

                NextExportPosition += ExportsDirectory.SerialSize;
                UassetFile.Seek(Start + 72);
            }


        }


        public void UpdateOffset()
        {

        }
    }
}
