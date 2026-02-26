namespace UAssetViewer.Models;

/// <summary>
/// 资产类型枚举
/// </summary>
public enum AssetType
{
    Unknown,
    DataTable,
    StringTable
}

/// <summary>
/// 扫描到的资产信息
/// </summary>
public class ScannedAsset
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileNameWithoutExtension(FilePath);
    public string DisplayName => FileName;
    public AssetType Type { get; set; } = AssetType.Unknown;
    public bool HasExpFile { get; set; }
    public int ItemCount { get; set; } // DataTable 行数或 StringTable 条目数
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 用于列表显示的摘要
    /// </summary>
    public string Summary
    {
        get
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                return $"⚠ {ErrorMessage}";
            string unit = Type == AssetType.StringTable ? "条" : "行";
            return $"{ItemCount} {unit}";
        }
    }
}