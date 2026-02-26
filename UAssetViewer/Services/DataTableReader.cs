using System.Data;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetViewer.Helpers;

namespace UAssetViewer.Services;

/// <summary>
/// DataTable 读取服务：将 DataTableExport 转为 System.Data.DataTable 用于 DataGrid 绑定
/// </summary>
public class DataTableReader
{
    private readonly EngineVersion _engineVersion;

    public DataTableReader(EngineVersion engineVersion = EngineVersion.VER_UE5_3)
    {
        _engineVersion = engineVersion;
    }

    /// <summary>
    /// 读取 DataTable 文件，返回 System.Data.DataTable
    /// </summary>
    public async Task<DataTable> ReadAsync(string filePath)
    {
        return await Task.Run(() => Read(filePath));
    }

    /// <summary>
    /// 同步读取
    /// </summary>
    public DataTable Read(string filePath)
    {
        var asset = new UAsset(filePath, _engineVersion);

        DataTableExport? dtExport = null;
        foreach (var export in asset.Exports)
        {
            if (export is DataTableExport dt)
            {
                dtExport = dt;
                break;
            }
        }

        if (dtExport?.Table?.Data == null || dtExport.Table.Data.Count == 0)
            throw new InvalidOperationException("该文件不包含有效的 DataTable 数据");

        var dataTable = new DataTable();

        // 第一列始终是 RowName
        dataTable.Columns.Add("RowName", typeof(string));

        // 从第一行推导所有列名
        var firstRow = dtExport.Table.Data[0];
        var columnNames = new List<string> { "RowName" };
        if (firstRow.Value != null)
        {
            foreach (var prop in firstRow.Value)
            {
                string colName = prop.Name?.Value?.Value ?? $"Col_{dataTable.Columns.Count}";
                // 防止重复列名
                string uniqueName = colName;
                int suffix = 1;
                while (columnNames.Contains(uniqueName))
                {
                    uniqueName = $"{colName}_{suffix++}";
                }
                columnNames.Add(uniqueName);
                dataTable.Columns.Add(uniqueName, typeof(string));
            }
        }

        // 填充行数据
        foreach (var row in dtExport.Table.Data)
        {
            var dataRow = dataTable.NewRow();
            dataRow["RowName"] = row.Name?.Value?.Value ?? "<unnamed>";

            if (row.Value != null)
            {
                for (int i = 0; i < row.Value.Count; i++)
                {
                    // +1 因为第一列是 RowName
                    if (i + 1 < dataTable.Columns.Count)
                    {
                        dataRow[i + 1] = PropertyFormatter.Format(row.Value[i]);
                    }
                }
            }

            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }

    /// <summary>
    /// 获取原始 PropertyData（用于 Tooltip 详细显示）
    /// </summary>
    public StructPropertyData? GetRowData(string filePath, int rowIndex)
    {
        var asset = new UAsset(filePath, _engineVersion);
        foreach (var export in asset.Exports)
        {
            if (export is DataTableExport dt && dt.Table?.Data != null)
            {
                if (rowIndex >= 0 && rowIndex < dt.Table.Data.Count)
                    return dt.Table.Data[rowIndex];
            }
        }
        return null;
    }
}