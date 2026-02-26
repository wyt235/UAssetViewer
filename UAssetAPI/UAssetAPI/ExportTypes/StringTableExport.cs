using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

namespace UAssetAPI.ExportTypes
{
    /// <summary>
    /// A string table. Holds Key->SourceString pairs of text.
    /// Optionally supports a third ArchiveName field per entry (custom engine modification).
    /// </summary>
    public class FStringTable : TMap<FString, FString>
    {
        [JsonProperty]
        public FString TableNamespace;

        /// <summary>
        /// Optional per-entry ArchiveName (custom engine modification).
        /// Key is the same FString key used in the main TMap.
        /// If empty, this is a standard StringTable with no ArchiveName column.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, FString> ArchiveNames = new Dictionary<string, FString>();

        /// <summary>
        /// Whether this StringTable has the custom ArchiveName field.
        /// </summary>
        [JsonIgnore]
        public bool HasArchiveName => ArchiveNames.Count > 0;

        public FStringTable(FString tableNamespace) : base()
        {
            TableNamespace = tableNamespace;
        }

        public FStringTable() : base()
        {

        }
    }

    /// <summary>
    /// Export data for a string table. See <see cref="FStringTable"/>.
    /// </summary>
    public class StringTableExport : NormalExport
    {
        [JsonProperty]
        public FStringTable Table;

        public StringTableExport(Export super) : base(super)
        {

        }

        public StringTableExport(FStringTable data, UAsset asset, byte[] extras) : base(asset, extras)
        {
            Table = data;
        }

        public StringTableExport()
        {

        }

        /// <summary>
        /// Safely skip one FString in the stream by reading only its length prefix
        /// and seeking past the payload. Returns false if the length is invalid.
        /// </summary>
        private static bool TrySkipFString(BinaryReader reader, long streamEnd)
        {
            if (reader.BaseStream.Position + 4 > streamEnd)
                return false;

            int length = reader.ReadInt32();
            long bytesToSkip;
            if (length > 0)
            {
                bytesToSkip = length; // UTF-8: length bytes (includes null terminator)
            }
            else if (length < 0)
            {
                bytesToSkip = (long)(-length) * 2; // UTF-16: -length chars * 2 bytes each
            }
            else
            {
                return true; // length == 0 => null FString, 0 payload bytes
            }

            // Sanity: the payload size must be reasonable and fit within the stream
            if (bytesToSkip < 0 || bytesToSkip > 100_000_000)
                return false;
            if (reader.BaseStream.Position + bytesToSkip > streamEnd)
                return false;

            reader.BaseStream.Seek(bytesToSkip, SeekOrigin.Current);
            return true;
        }

        public override void Read(AssetBinaryReader reader, int nextStarting)
        {
            base.Read(reader, nextStarting);

            // Read TableNamespace
            FString tableNamespace = reader.ReadFString();
            int numEntries = reader.ReadInt32();
            long entriesStart = reader.BaseStream.Position;

            // -------------------------------------------------------------------
            // Detect whether the binary data uses 3 fields per entry (custom) or 2 (standard).
            // Strategy: Skip N*2 FStrings (lightweight: only read length + seek).
            //   If the final position matches nextStarting, it's 2-field mode.
            //   Otherwise try N*3 FStrings. If that matches, it's 3-field mode.
            //   This avoids calling ReadFString() which may StackOverflow on bad lengths.
            // -------------------------------------------------------------------
            bool use3Fields = false;

            if (numEntries > 0)
            {
                long streamEnd = reader.BaseStream.Length;

                // Try 2-field mode first
                reader.BaseStream.Position = entriesStart;
                bool ok2 = true;
                for (int i = 0; i < numEntries * 2; i++)
                {
                    if (!TrySkipFString(reader, streamEnd))
                    {
                        ok2 = false;
                        break;
                    }
                }
                long end2 = reader.BaseStream.Position;
                // Allow a small tolerance (0~8 bytes) for export trailer/extras at the end
                long diff2 = nextStarting - end2;
                bool match2 = ok2 && (diff2 >= 0 && diff2 <= 8);

                // Try 3-field mode
                reader.BaseStream.Position = entriesStart;
                bool ok3 = true;
                for (int i = 0; i < numEntries * 3; i++)
                {
                    if (!TrySkipFString(reader, streamEnd))
                    {
                        ok3 = false;
                        break;
                    }
                }
                long end3 = reader.BaseStream.Position;
                long diff3 = nextStarting - end3;
                bool match3 = ok3 && (diff3 >= 0 && diff3 <= 8);

                // Decide: pick whichever mode gets closer to nextStarting
                if (match3 && match2)
                {
                    // Both modes fit within tolerance; pick the one closer to nextStarting
                    use3Fields = diff3 < diff2;
                }
                else if (match3 && !match2)
                    use3Fields = true;
                else if (match2 && !match3)
                    use3Fields = false;
                // else neither matched: fall back to 2-field mode

                // Reset to re-read entries for real
                reader.BaseStream.Position = entriesStart;
            }

            Table = new FStringTable(tableNamespace);

            if (use3Fields)
            {
                // 3-field mode: Key, Value, ArchiveName
                for (int i = 0; i < numEntries; i++)
                {
                    FString key = reader.ReadFString();
                    FString value = reader.ReadFString();
                    FString archiveName = reader.ReadFString();

                    Table.Add(key, value);
                    string keyStr = key?.Value ?? $"__key_{i}";
                    Table.ArchiveNames[keyStr] = archiveName;
                }
            }
            else
            {
                // Standard 2-field mode: Key, Value
                for (int i = 0; i < numEntries; i++)
                {
                    Table.Add(reader.ReadFString(), reader.ReadFString());
                }
            }
        }

        public override void Write(AssetBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(Table.TableNamespace);
            writer.Write(Table.Count);
            for (int i = 0; i < Table.Count; i++)
            {
                FString key = Table.Keys.ElementAt(i);
                writer.Write(key);
                writer.Write(Table[i]);

                // Write ArchiveName if present
                if (Table.HasArchiveName)
                {
                    string keyStr = key?.Value ?? $"__key_{i}";
                    if (Table.ArchiveNames.TryGetValue(keyStr, out FString archiveName))
                    {
                        writer.Write(archiveName);
                    }
                    else
                    {
                        writer.Write(new FString());
                    }
                }
            }
        }
    }
}
