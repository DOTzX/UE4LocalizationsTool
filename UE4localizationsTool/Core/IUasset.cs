using AssetParser.Object;
using Helper.MemoryList;
using System.Collections.Generic;

namespace AssetParser
{
    public interface IUasset
    {
        int LegacyFileVersion { get; set; }
        UEVersions EngineVersion { get; set; }
        EPackageFlags PackageFlags { get; set; }
        int TotalHeaderSize { get; set; }
        int NameCount { get; set; }
        int NameOffset { get; set; }
        int ExportCount { get; set; }
        int ExportOffset { get; set; }
        int ImportCount { get; set; }
        int ImportOffset { get; set; }
        List<string> NameMap { get; set; }
        List<ImportsDirectory> ImportMap { get; set; }
        List<ExportsDirectory> ExportMap { get; set; }
        MemoryList UassetFile { get; set; }
        bool IOFile { get; set; }
        bool IsNotUseUexp { get; set; }
        bool UseFromStruct { get; set; }
        bool AutoVersion { get; set; }
        bool UseMethod2 { get; set; }
        bool UseWithLocRes { get; set; }
        int PathCount { get; set; }
        bool PathModify { get; set; }
        void EditName(string NewStr, int Index);
        void ExportReadOrEdit(bool Modify = false);
        void UpdateOffset();
    }
}