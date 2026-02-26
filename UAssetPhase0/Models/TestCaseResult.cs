namespace UAssetPhase0.Models;

/// <summary>
/// 单个验证用例的结果
/// </summary>
public class TestCaseResult
{
    public string TestCaseId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SampleFile { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long ElapsedMs { get; set; }

    public override string ToString()
    {
        string status = Passed ? "✅ PASS" : "❌ FAIL";
        string result = $"  [{status}] {TestCaseId} - {Description}";
        if (ElapsedMs > 0)
            result += $" ({ElapsedMs}ms)";
        if (!string.IsNullOrEmpty(Details))
            result += $"\n           {Details}";
        if (!string.IsNullOrEmpty(ErrorMessage))
            result += $"\n           错误: {ErrorMessage}";
        return result;
    }
}

/// <summary>
/// 资产扫描信息
/// </summary>
public class AssetInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileNameWithoutExtension(FilePath);
    public string AssetType { get; set; } = "Unknown";
    public bool HasExpFile { get; set; }
    public string? ExpFilePath { get; set; }
}
