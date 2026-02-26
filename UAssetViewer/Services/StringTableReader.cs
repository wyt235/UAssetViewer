using System.Data;
using System.Diagnostics;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;

namespace UAssetViewer.Services;

/// <summary>
/// StringTable 读取服务：将 StringTableExport 转为 System.Data.DataTable
/// 自动兼容标准 StringTable（2列）和魔改 StringTable（3列，含 ArchiveName）
/// </summary>
public class StringTableReader
{
    private readonly EngineVersion _engineVersion;

    public StringTableReader(EngineVersion engineVersion = EngineVersion.VER_UE5_3)
    {
        _engineVersion = engineVersion;
    }

    /// <summary>
    /// 异步读取
    /// </summary>
    public async Task<(DataTable Table, string? Namespace)> ReadAsync(string filePath)
    {
        return await Task.Run(() => Read(filePath));
    }

    /// <summary>
    /// 同步读取，返回 DataTable + TableNamespace
    /// </summary>
    public (DataTable Table, string? Namespace) Read(string filePath)
    {
        var asset = new UAsset(filePath, _engineVersion);

        StringTableExport? stExport = null;
        foreach (var export in asset.Exports)
        {
            if (export is StringTableExport st)
            {
                stExport = st;
                break;
            }
        }

        if (stExport?.Table == null)
            throw new InvalidOperationException("该文件不包含有效的 StringTable 数据");

        var table = stExport.Table;
        string? ns = table.TableNamespace?.Value;
        bool hasArchive = table.HasArchiveName;

        // 诊断输出
        Console.Error.WriteLine($"[StringTableReader] File={filePath}");
        Console.Error.WriteLine($"[StringTableReader] Namespace={ns}, EntryCount={table.Count}, HasArchiveName={hasArchive}");
        foreach (var kvp in table)
        {
            string k = kvp.Key?.Value ?? "<null>";
            string v = kvp.Value?.Value ?? "<null>";
            string a = "";
            if (hasArchive && table.ArchiveNames.TryGetValue(k, out var an))
                a = an?.Value ?? "<null>";
            Console.Error.WriteLine($"  [{k}] => [{v}] | Archive=[{a}]");
        }

        var dataTable = new DataTable();
        dataTable.Columns.Add("Key", typeof(string));
        dataTable.Columns.Add("Value", typeof(string));

        if (hasArchive)
        {
            dataTable.Columns.Add("ArchiveName", typeof(string));
        }

        foreach (var kvp in table)
        {
            var row = dataTable.NewRow();
            string keyStr = kvp.Key?.Value ?? "<null>";
            row["Key"] = keyStr;
            row["Value"] = kvp.Value?.Value ?? "<null>";

            if (hasArchive)
            {
                if (table.ArchiveNames.TryGetValue(keyStr, out var archiveName))
                    row["ArchiveName"] = archiveName?.Value ?? "";
                else
                    row["ArchiveName"] = "";
            }

            dataTable.Rows.Add(row);
        }

        return (dataTable, ns);
    }
}