using System.Collections.Generic;

namespace AssetParser.Object
{
    public struct ImportsDirectory
    {

        public string ClassPackage { get; set; }
        public string ClassName { get; set; }
        public int OuterIndex { get; set; }
        public string ObjectName { get; set; }
        public string PackageName { get; set; }
        public bool ImportOptional { get; set; }

    }
    public struct ExportsDirectory
    {
        public int ClassIndex { get; set; }
        public int SuperIndex { get; set; }
        public int TemplateIndex { get; set; }
        public int OuterIndex { get; set; }
        public string ObjectName { get; set; }
        public short ExportMemberType { get; set; }
        public int SerialSize { get; set; }
        public int SerialOffset { get; set; }

        public List<byte> ExportData;
    }



}
