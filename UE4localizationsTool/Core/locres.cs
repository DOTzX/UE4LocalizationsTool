﻿using Helper.MemoryList;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetParser
{
    public class locres
    {
        public enum LocresVersion : byte
        {
            Legacy = 0,
            Compact,
            Optimized,
            Optimized_CityHash64_UTF16,
        }

        //{7574140E-4A67-FC03-4A15-909DC3377F1B}
        private readonly byte[] MagicGUID = { (byte)0x0E, (byte)0x14, (byte)0x74, (byte)0x75, (byte)0x67, (byte)0x4A, (byte)0x03, (byte)0xFC, (byte)0x4A, (byte)0x15, (byte)0x90, (byte)0x9D, (byte)0xC3, (byte)0x37, (byte)0x7F, (byte)0x1B };

        public List<List<string>> Strings;
        public int CurrentIndex;
        MemoryList locresData;
        public locres(string FilePath)
        {
            Strings = new List<List<string>>();//[Text id,Text Value,...] -> Text id = TextIndex
            locresData = new MemoryList(FilePath);
            ReadOrEdit();

        }

        private void ReadOrEdit(bool Modify = false)
        {
            locresData.Seek(0);
            byte[] FileGUID = locresData.GetBytes(16);
            LocresVersion Version;
            if (FileGUID.SequenceEqual(MagicGUID))
            {
                Version = (LocresVersion)locresData.GetByteValue();
            }
            else
            {
                Version = LocresVersion.Legacy;
                locresData.Seek(0);
            }

            if (Version > LocresVersion.Optimized_CityHash64_UTF16)
            {
                throw new Exception("Unsupported locres version");
            }

            // Console.WriteLine(Version);

            if (Version >= LocresVersion.Compact)
            {
                // Console.WriteLine("Compact");

                int localizedStringOffset = (int)locresData.GetInt64Value();
                int currentFileOffset = locresData.GetPosition();


                if (localizedStringOffset == -1)
                {
                    return;
                }

                locresData.Seek(localizedStringOffset);

                int localizedStringCount = locresData.GetIntValue();

                if (Version >= LocresVersion.Optimized)
                {
                    for (int i = 0; i < localizedStringCount; i++)
                    {
                        if (!Modify)
                        {
                            Strings.Add(new List<string>() { Strings.Count.ToString(), locresData.GetStringUE() });
                            locresData.Skip(4);
                        }
                        else
                        {
                            locresData.ReplaceStringUE(Strings[CurrentIndex][1]);
                            CurrentIndex++;
                            locresData.Skip(4);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < localizedStringCount; i++)
                    {
                        if (!Modify)
                        {
                            Strings.Add(new List<string>() { Strings.Count.ToString(), locresData.GetStringUE() });
                        }
                        else
                        {

                            locresData.ReplaceStringUE(Strings[CurrentIndex][1]);
                            CurrentIndex++;
                        }
                    }
                }
                locresData.Seek(currentFileOffset);

            }
            else if (Version == LocresVersion.Legacy)
            {
                int HashTablesCount = locresData.GetIntValue();


                for (int i = 0; i < HashTablesCount; i++)
                {
                    locresData.GetStringUE(); //hash namespace

                    int localizedStringCount = locresData.GetIntValue();


                    for (int n = 0; n < localizedStringCount; n++)
                    {
                        if (!Modify)
                        {
                            string KeyHash = locresData.GetStringUE(); //string hash
                            locresData.Skip(4); //Unkown
                            Strings.Add(new List<string>() { KeyHash, locresData.GetStringUE() });

                        }
                        else
                        {
                            locresData.GetStringUE(); //string hash
                            locresData.Skip(4); //Unkown
                            locresData.ReplaceStringUE(Strings[CurrentIndex][1]);
                            CurrentIndex++;
                        }
                    }

                }
                return;
            }


            if (Version >= LocresVersion.Optimized)
            {
                locresData.Skip(4); //FileHash
            }


            int namespaceCount = locresData.GetIntValue();

            for (int n = 0; n < namespaceCount; n++)
            {
                string nameSpaceStr;
                uint StrHash;
                ReadTextKey(locresData, Version, out StrHash, out nameSpaceStr); //no need right now
                uint keyCount = locresData.GetUIntValue();
                for (int k = 0; k < keyCount; k++)
                {
                    string KeyStr;
                    uint KeyStrHash;
                    ReadTextKey(locresData, Version, out KeyStrHash, out KeyStr);
                    locresData.Skip(4);//SourceStringHash

                    if (Version >= LocresVersion.Compact)
                    {
                        int localizedStringIndex = locresData.GetIntValue();
                        if (Strings.Count > localizedStringIndex)
                        {
                            Strings[localizedStringIndex][0] = KeyStr;
                        }

                    }

                }

            }










        }

        private void ReadTextKey(MemoryList memoryList, LocresVersion locresVersion, out uint StrHash, out string Str)
        {
            StrHash = 0;
            Str = "";
            if (locresVersion >= LocresVersion.Optimized)
            {
                StrHash = memoryList.GetUIntValue();
            }

            Str = memoryList.GetStringUE();
        }


        private void ModifyStrings()
        {
            CurrentIndex = 0;
            ReadOrEdit(true);
        }

        public void SaveFile(string FilPath)
        {
            ModifyStrings();
            locresData.WriteFile(FilPath);
        }





    }
}
