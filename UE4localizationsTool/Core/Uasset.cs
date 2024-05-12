using AssetParser.Object;
using Helper.MemoryList;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetParser {
    public class Uasset : IUasset {
        List<int> OffsetsList = new List<int>();
        private int NewSize;
        public int LegacyFileVersion { get; set; }
        public UEVersions EngineVersion { get; set; }
        int numCustomVersions;
        public EPackageFlags PackageFlags { get; set; }
        public int TotalHeaderSize { get; set; }
        public int NameCount { get; set; }
        public int NameOffset { get; set; }
        public int ExportCount { get; set; }
        public int GatherableTextDataCount;
        public int GatherableTextDataOffset;
        public int ExportOffset { get; set; }
        public int ImportCount { get; set; }
        public int ImportOffset { get; set; }
        public int DependsOffset;
        public int SoftPackageReferencesCount;
        public int SoftPackageReferencesOffset;
        public int SearchableNamesOffset;
        public int ThumbnailTableOffset;
        public int AssetRegistryDataOffset;
        private int FBulkDataStartOffset;
        public long BulkDataStartOffset;
        public int WorldTileInfoDataOffset;
        public int PreloadDependencyCount;
        public int PreloadDependencyOffset;
        public List<string> NameMap { get; set; }
        public List<ImportsDirectory> ImportMap { get; set; }
        public List<ExportsDirectory> ExportMap { get; set; }
        public MemoryList UassetFile { get; set; }
        public MemoryList UexpFile { get; set; }
        public bool IsNotUseUexp { get; set; }
        public bool UseFromStruct { get; set; } = true;
        public bool AutoVersion { get; set; } = false;
        public bool IOFile { get; set; } = false;
        public int PathCount { get; set; } = 0;
        public bool PathModify { get; set; } = true;
        public bool UseMethod2 { get; set; } = false;
        public bool UseWithLocRes { get; set; } = false;

        public Uasset(string FilePath) {
            UassetFile = new MemoryList(FilePath);

            //if (UassetFile.GetUIntValue(false) != 0x9E2A83C1u) {
            //    return;
            //}

            if (UassetFile.GetIntValue(false, UassetFile.GetSize() - 4) != -1641380927) {
                if (!File.Exists(Path.ChangeExtension(FilePath, ".uexp"))) {
                    throw new Exception("Uexp file is not exists!");
                }
                UexpFile = new MemoryList(Path.ChangeExtension(FilePath, ".uexp"));
                IsNotUseUexp = false;
            } else {
                IsNotUseUexp = true;
            }

            ReadHeader();

            // seek to position
            UassetFile.Seek(NameOffset, SeekOrigin.Begin);

            // Get Names
            NameMap = new List<string>();
            for (int n = 0; n < NameCount; n++) {
                NameMap.Add(UassetFile.GetStringUE());

                if (NameMap[n].Contains(@"/") && PathModify) {
                    PathCount++;
                } else {
                    PathModify = false;
                }

                //Flags
                if (EngineVersion >= UEVersions.VER_UE4_NAME_HASHES_SERIALIZED) {
                    UassetFile.Skip(2); // NonCasePreservingHash
                    UassetFile.Skip(2); // CasePreservingHash
                }
            }

            // seek to position
            UassetFile.Seek(ImportOffset, SeekOrigin.Begin);

            // Get Imports
            ImportMap = new List<ImportsDirectory>();
            for (int n = 0; n < ImportCount; n++) {
                ImportsDirectory ImportsDirectory = new ImportsDirectory();
                ImportsDirectory.ClassPackage = UassetFile.GetFName(NameMap);
                ImportsDirectory.ClassName = UassetFile.GetFName(NameMap);
                ImportsDirectory.OuterIndex = UassetFile.GetIntValue();
                ImportsDirectory.ObjectName = UassetFile.GetFName(NameMap);

                if (EngineVersion >= UEVersions.VER_UE4_NON_OUTER_PACKAGE_IMPORT && !PackageFlags.HasFlag(EPackageFlags.PKG_FilterEditorOnly)) {
                    ImportsDirectory.PackageName = UassetFile.GetFName(NameMap);
                }

                ImportsDirectory.ImportOptional = (EngineVersion >= UEVersions.OPTIONAL_RESOURCES && UassetFile.GetBool32Value());

                ImportMap.Add(ImportsDirectory);
            }

            // Get Exports
            ExportMap = new List<ExportsDirectory>();
            ExportReadOrEdit();
        }

        private void ReadHeader(bool saveoffsets = true) {
            UassetFile.Seek(0);
            UassetFile.Skip(4); // Unreal Signature

            LegacyFileVersion = UassetFile.GetIntValue();
            if (LegacyFileVersion != -4) {
                UassetFile.GetIntValue(); // LegacyUE3Version
            }

            EngineVersion = (UEVersions) UassetFile.GetIntValue();

            if (LegacyFileVersion <= -8) // CurrentLegacyFileVersion = -8
            {
                int FileVersionUE5 = UassetFile.GetIntValue();
                if (FileVersionUE5 > 0)
                    EngineVersion = (UEVersions) FileVersionUE5;
                else
                    EngineVersion = UEVersions.VER_UE5_0;
            }

            UassetFile.Skip(4); // FileVersionLicenseeUE 
            if (LegacyFileVersion <= -2) {
                numCustomVersions = UassetFile.GetIntValue();
                for (int i = 0; i < numCustomVersions; i++) {
                    UassetFile.Skip(16); // Guid
                    UassetFile.Skip(4); // Unknown 
                }
            }

            // File Start
            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            TotalHeaderSize = UassetFile.GetIntValue();

            // None
            UassetFile.GetStringUE(); // FolderName

            // Package Flags
            PackageFlags = (EPackageFlags) UassetFile.GetUIntValue();

            // Property Names
            NameCount = UassetFile.GetIntValue();

            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            NameOffset = UassetFile.GetIntValue();

            //TODO
            if (EngineVersion == UEVersions.UNKNOWN) {
                if (NameOffset - (numCustomVersions * 20) == 189) {
                    EngineVersion = UEVersions.VER_UE4_15;
                    AutoVersion = true;
                } else if (NameOffset - (numCustomVersions * 20) > 185) {
                    EngineVersion = UEVersions.VER_UE4_16;
                    AutoVersion = true;
                } else if (NameOffset - (numCustomVersions * 20) == 185) {
                    EngineVersion = UEVersions.VER_UE4_6;
                    AutoVersion = true;
                }
            }

            UseFromStruct = EngineVersion >= UEVersions.VER_UE4_NAME_HASHES_SERIALIZED;

            if (EngineVersion >= UEVersions.ADD_SOFTOBJECTPATH_LIST) {
                int SoftObjectPathsCount = UassetFile.GetIntValue();
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                int SoftObjectPathsOffset = UassetFile.GetIntValue();
            }

            if (!PackageFlags.HasFlag(EPackageFlags.PKG_FilterEditorOnly)) {
                if (EngineVersion >= UEVersions.VER_UE4_ADDED_PACKAGE_SUMMARY_LOCALIZATION_ID) {
                    UassetFile.GetStringUE(); // LocalizationId
                }
            }

            if (EngineVersion >= UEVersions.VER_UE4_SERIALIZE_TEXT_IN_PACKAGES) {
                GatherableTextDataCount = UassetFile.GetIntValue();
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                GatherableTextDataOffset = UassetFile.GetIntValue();
            }

            // Exports Blocks
            ExportCount = UassetFile.GetIntValue();
            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            ExportOffset = UassetFile.GetIntValue();

            // Imports Blocks
            ImportCount = UassetFile.GetIntValue();
            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            ImportOffset = UassetFile.GetIntValue();

            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            DependsOffset = UassetFile.GetIntValue();

            if (EngineVersion < UEVersions.VER_UE4_OLDEST_LOADABLE_PACKAGE || EngineVersion >= UEVersions.AUTOMATIC_VERSION) {
                return;
            }

            if (EngineVersion >= UEVersions.VER_UE4_ADD_STRING_ASSET_REFERENCES_MAP) {
                SoftPackageReferencesCount = UassetFile.GetIntValue();
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                SoftPackageReferencesOffset = UassetFile.GetIntValue();
            }
            if (EngineVersion >= UEVersions.VER_UE4_ADDED_SEARCHABLE_NAMES) {
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                SearchableNamesOffset = UassetFile.GetIntValue();
            }
            if (saveoffsets)
                OffsetsList.Add(UassetFile.GetPosition());
            ThumbnailTableOffset = UassetFile.GetIntValue();

            // PackageGuid
            UassetFile.Skip(16); // Guid

            int Num = UassetFile.GetIntValue();
            for (int i = 0; i < Num; i++) {
                UassetFile.Skip(8);
            }

            if (EngineVersion >= UEVersions.VER_UE4_ENGINE_VERSION_OBJECT) {
                UassetFile.Skip(2); // Major
                UassetFile.Skip(2); // Minor
                UassetFile.Skip(2); // Patch
                UassetFile.Skip(4); // changelist
                UassetFile.GetStringUE(); // branch
            } else {
                UassetFile.Skip(4); // engineChangelist
            }

            if (EngineVersion >= UEVersions.VER_UE4_PACKAGE_SUMMARY_HAS_COMPATIBLE_ENGINE_VERSION) {
                UassetFile.Skip(2); // Major
                UassetFile.Skip(2); // Minor
                UassetFile.Skip(2); // Patch
                UassetFile.Skip(4); // changelist
                UassetFile.GetStringUE(); // branch
            }

            UassetFile.Skip(4); // CompressionFlags

            UassetFile.Skip(4); // numCompressedChunks

            UassetFile.Skip(4); // PackageSource

            UassetFile.Skip(4); // numAdditionalPackagesToCook

            if (LegacyFileVersion > -7) {
                UassetFile.Skip(4); // numTextureAllocations 
            }

            if (EngineVersion >= UEVersions.VER_UE4_ASSET_REGISTRY_TAGS) {
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                AssetRegistryDataOffset = UassetFile.GetIntValue();
            }

            if (EngineVersion >= UEVersions.VER_UE4_SUMMARY_HAS_BULKDATA_OFFSET) {
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                FBulkDataStartOffset = UassetFile.GetPosition();
                BulkDataStartOffset = UassetFile.GetInt64Value();
            }

            if (EngineVersion >= UEVersions.VER_UE4_WORLD_LEVEL_INFO) {
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                WorldTileInfoDataOffset = UassetFile.GetIntValue();
            }

            //ChunkIDs
            if (EngineVersion >= UEVersions.VER_UE4_CHANGED_CHUNKID_TO_BE_AN_ARRAY_OF_CHUNKIDS) {
                int numChunkIDs = UassetFile.GetIntValue();

                for (int i = 0; i < numChunkIDs; i++) {
                    UassetFile.Skip(4);
                }
            } else if (EngineVersion >= UEVersions.VER_UE4_ADDED_CHUNKID_TO_ASSETDATA_AND_UPACKAGE) {
                UassetFile.Skip(4); // chunkId
            }

            if (EngineVersion >= UEVersions.VER_UE4_PRELOAD_DEPENDENCIES_IN_COOKED_EXPORTS) {
                PreloadDependencyCount = UassetFile.GetIntValue();
                if (saveoffsets)
                    OffsetsList.Add(UassetFile.GetPosition());
                PreloadDependencyOffset = UassetFile.GetIntValue();
            }

        }

        public void EditName(string NewStr, int Index) {
            if (NameMap[Index] == NewStr) {
                return;
            }

            NewSize = 0;
            int OldSize = UassetFile.GetSize();
            UassetFile.Seek(NameOffset, SeekOrigin.Begin);

            for (int n = 0; n < NameCount; n++) {
                if (n == Index) {
                    UassetFile.ReplaceStringUE(NewStr);
                    NameMap[Index] = NewStr;
                    if (EngineVersion >= UEVersions.VER_UE4_NAME_HASHES_SERIALIZED) {
                        UassetFile.Skip(2); // NonCasePreservingHash
                        UassetFile.Skip(2); // CasePreservingHash
                    }
                    break;
                } else {
                    UassetFile.GetStringUE();
                    if (EngineVersion >= UEVersions.VER_UE4_NAME_HASHES_SERIALIZED) {
                        UassetFile.Skip(2); // NonCasePreservingHash
                        UassetFile.Skip(2); // CasePreservingHash
                    }
                }
            }

            NewSize = UassetFile.GetSize() - OldSize;

            foreach (int offset in OffsetsList) {
                var OldOffsetValue = UassetFile.GetIntValue(false, offset);
                if (OldOffsetValue > NameOffset) {
                    int NewOffsetValue = OldOffsetValue + NewSize;
                    UassetFile.SetIntValue(NewOffsetValue, false, offset);
                }
            }

            ReadHeader(false);
        }



        public void ExportReadOrEdit(bool Modify = false) {
            int NextExportPosition = TotalHeaderSize;

            //seek to position
            UassetFile.Seek(ExportOffset, SeekOrigin.Begin);

            for (int n = 0; n < ExportCount; n++) {
                ExportsDirectory ExportsDirectory = new ExportsDirectory();
                ExportsDirectory.ClassIndex = UassetFile.GetIntValue();
                ExportsDirectory.SuperIndex = UassetFile.GetIntValue();
                if (EngineVersion >= UEVersions.VER_UE4_TemplateIndex_IN_COOKED_EXPORTS) {
                    ExportsDirectory.TemplateIndex = UassetFile.GetIntValue();
                }

                ExportsDirectory.OuterIndex = UassetFile.GetIntValue();
                ExportsDirectory.ObjectName = UassetFile.GetFName(NameMap);

                _ = UassetFile.GetUIntValue(); // ObjectFlags

                if (EngineVersion < UEVersions.VER_UE4_64BIT_EXPORTMAP_SERIALSIZES) {
                    if (!Modify) {
                        ExportsDirectory.SerialSize = UassetFile.GetIntValue();
                        ExportsDirectory.SerialOffset = UassetFile.GetIntValue();
                    } else {
                        UassetFile.SetIntValue(ExportMap[n].ExportData.Count); // SerialSize
                        UassetFile.SetIntValue(NextExportPosition); // SerialOffset
                        NextExportPosition += ExportMap[n].ExportData.Count;
                    }
                } else {
                    if (!Modify) {
                        ExportsDirectory.SerialSize = UassetFile.GetIntValue(); // (long)
                    } else {
                        UassetFile.SetIntValue(ExportMap[n].ExportData.Count); // (long)
                    }
                    UassetFile.Skip(4);

                    if (!Modify) {
                        ExportsDirectory.SerialOffset = UassetFile.GetIntValue(); // (long)
                    } else {
                        UassetFile.SetIntValue(NextExportPosition); // (long)
                        NextExportPosition += ExportMap[n].ExportData.Count;
                    }
                    UassetFile.Skip(4);
                }

                UassetFile.Skip(4 * 3); // GetBool32Value() * 3 = ForcedExport + NotForClient + NotForServer

                if (EngineVersion < UEVersions.REMOVE_OBJECT_EXPORT_PACKAGE_GUID) UassetFile.Skip(16); // PackageGuid

                var IsInheritedInstance = (EngineVersion >= UEVersions.TRACK_OBJECT_EXPORT_IS_INHERITED && UassetFile.GetBool32Value());
                UassetFile.Skip(4); // PackageFlags = GetUIntValue()
                var NotAlwaysLoadedForEditorGame = (EngineVersion >= UEVersions.VER_UE4_LOAD_FOR_EDITOR_GAME && UassetFile.GetBool32Value());
                var IsAsset = (EngineVersion >= UEVersions.VER_UE4_COOKED_ASSETS_IN_EDITOR_SUPPORT && UassetFile.GetBool32Value());
                var GeneratePublicHash = (EngineVersion >= UEVersions.OPTIONAL_RESOURCES && UassetFile.GetBool32Value());

                if (EngineVersion >= UEVersions.VER_UE4_PRELOAD_DEPENDENCIES_IN_COOKED_EXPORTS) {
                    UassetFile.Skip(4 * 5); // GeIntValue() * 5 = FirstExportDependency + SerializationBeforeSerializationDependencies +
                                            // CreateBeforeSerializationDependencies + SerializationBeforeCreateDependencies + CreateBeforeCreateDependencies
                }

                if (EngineVersion >= UEVersions.SCRIPT_SERIALIZATION_OFFSET) {
                    throw new Exception("https://github.com/FabianFG/CUE4Parse/blob/cbb2af97f968b29c0c30347f225a6b27ac9917f9/CUE4Parse/UE4/Objects/UObject/ObjectResource.cs#L245-L249");
                }

                //if (DependsOffset > 0 && ExportCount > 0) {
                //    UassetFile.Seek(DependsOffset, SeekOrigin.Begin);
                //    // DependsMap
                //    for (int n1 = 0; n1 < ExportCount; n1++) {
                //        UassetFile.Skip(4); // Index(4)
                //    }
                //}

                //if (PreloadDependencyCount > 0 && PreloadDependencyOffset > 0) {
                //    UassetFile.Seek(PreloadDependencyOffset, SeekOrigin.Begin);
                //    // PreloadDependencies
                //    for (int n2 = 0; n2 < PreloadDependencyCount; n2++) {
                //        UassetFile.Skip(4); // Index(4)
                //    }
                //}

                //if (DataResourceOffset > 0) {
                //    UassetFile.Seek(DataResourceOffset, SeekOrigin.Begin);
                //    var dataResourceVersion = (EObjectDataResourceVersion) uassetAr.Read<uint>();
                //    if (dataResourceVersion > EObjectDataResourceVersion.Invalid && dataResourceVersion <= EObjectDataResourceVersion.Latest) {
                //        DataResourceMap = uassetAr.ReadArray(() => new FObjectDataResource(uassetAr));
                //    }
                //}

                if (!Modify) {
                    if (IsNotUseUexp) {
                        ExportsDirectory.ExportData = new List<byte>();
                        ExportsDirectory.ExportData.AddRange(UassetFile.GetBytes(ExportsDirectory.SerialSize, false, ExportsDirectory.SerialOffset));
                        ExportMap.Add(ExportsDirectory);
                    } else {
                        UexpFile.Seek(ExportsDirectory.SerialOffset - TotalHeaderSize, SeekOrigin.Begin);
                        ExportsDirectory.ExportData = new List<byte>();
                        ExportsDirectory.ExportData.AddRange(UexpFile.GetBytes(ExportsDirectory.SerialSize));
                        ExportMap.Add(ExportsDirectory);
                    }
                }
            }
        }

        int ExportSize() {
            int Totalsize = 0;
            foreach (ExportsDirectory Size in ExportMap) {
                Totalsize += Size.ExportData.Count;
            }
            return Totalsize;
        }

        public void UpdateOffset() {
            //for textures 🤔
            if (FBulkDataStartOffset > 0 && BulkDataStartOffset > 0) {
                UassetFile.SetIntValue(IsNotUseUexp ? UassetFile.GetSize() : UassetFile.GetSize() + ExportSize(), false, FBulkDataStartOffset);
            }
        }

    }
}
