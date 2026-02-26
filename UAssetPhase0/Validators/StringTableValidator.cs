using System.Diagnostics;
using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetPhase0.Models;

namespace UAssetPhase0.Validators;

/// <summary>
/// StringTable 验证器：验证 UAssetAPI 对 StringTable 类型 .uasset 的解析能力
/// </summary>
public static class StringTableValidator
{
    /// <summary>
    /// TC-007: StringTable 类型识别 — 验证 Exports 中是否包含 StringTableExport
    /// </summary>
    public static (TestCaseResult Result, UAsset? Asset, StringTableExport? Export) TC007_TypeIdentification(
        string filePath, EngineVersion version)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-007",
            Description = "StringTable 类型识别",
            SampleFile = Path.GetFileName(filePath)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var asset = new UAsset(filePath, version);
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;

            var sb = new StringBuilder();
            StringTableExport? stExport = null;

            for (int i = 0; i < asset.Exports.Count; i++)
            {
                var export = asset.Exports[i];
                sb.AppendLine($"    Export[{i}]: {export.GetType().Name} (ClassIndex: {export.ClassIndex})");

                if (export is StringTableExport st)
                {
                    stExport = st;
                }
            }

            result.Passed = stExport != null;
            result.Details = $"加载耗时: {sw.ElapsedMilliseconds}ms\n{sb.ToString().TrimEnd()}";

            if (!result.Passed)
                result.ErrorMessage = "未找到 StringTableExport 类型的 Export";

            return (result, asset, stExport);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            return (result, null, null);
        }
    }

    /// <summary>
    /// TC-008: StringTable 内容读取 — 读取键值对并输出
    /// </summary>
    public static TestCaseResult TC008_ContentRead(StringTableExport stExport, string filePath,
        int? expectedCount = null, int showTopN = 10)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-008",
            Description = "StringTable 内容读取",
            SampleFile = Path.GetFileName(filePath)
        };

        try
        {
            var sb = new StringBuilder();
            int totalCount = 0;

            var table = stExport.Table;
            if (table != null)
            {
                sb.AppendLine($"    StringTable.TableNamespace: {table.TableNamespace}");

                // FStringTable 继承自 TMap<FString, FString>
                // 直接通过 Count 和 GetEnumerator 访问数据
                totalCount = table.Count;
                sb.AppendLine($"    条目总数: {totalCount}");

                int shown = 0;
                foreach (var kvp in table)
                {
                    if (shown >= showTopN) break;
                    sb.AppendLine($"      [{shown}] Key=\"{kvp.Key}\"  Value=\"{kvp.Value}\"");
                    shown++;
                }

                if (totalCount > showTopN)
                    sb.AppendLine($"      ... 还有 {totalCount - showTopN} 条未显示");
            }
            else
            {
                sb.AppendLine("    ⚠️ Table 属性为 null");
            }

            // 判定结果
            if (expectedCount.HasValue)
            {
                result.Passed = totalCount == expectedCount.Value;
                if (!result.Passed)
                    result.ErrorMessage = $"条目数不匹配: 期望 {expectedCount.Value}, 实际 {totalCount}";
            }
            else
            {
                result.Passed = totalCount > 0;
                if (!result.Passed)
                    result.ErrorMessage = "StringTable 条目数为 0 或无法读取";
            }

            result.Details = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// TC-009: 中文字符串支持 — 检查是否存在中文字符且未出现乱码
    /// </summary>
    public static TestCaseResult TC009_ChineseSupport(StringTableExport stExport, string filePath)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-009",
            Description = "StringTable 中文字符串支持",
            SampleFile = Path.GetFileName(filePath)
        };

        try
        {
            var table = stExport.Table;
            if (table == null || table.Count == 0)
            {
                result.Passed = false;
                result.ErrorMessage = "无法读取 StringTable 数据";
                return result;
            }

            var sb = new StringBuilder();
            int chineseEntries = 0;
            int suspiciousEntries = 0;
            var chineseSamples = new List<string>();

            // FStringTable 继承自 TMap<FString, FString>
            // kvp.Key 和 kvp.Value 都是 FString，通过 .Value 获取 string
            foreach (var kvp in table)
            {
                string value = kvp.Value?.Value ?? "";

                // 检查是否包含中文字符
                bool hasChinese = value.Any(c => c >= 0x4E00 && c <= 0x9FFF);
                if (hasChinese)
                {
                    chineseEntries++;
                    if (chineseSamples.Count < 5)
                    {
                        string keyStr = kvp.Key?.Value ?? "<null>";
                        chineseSamples.Add($"Key=\"{keyStr}\"  Value=\"{(value.Length > 60 ? value[..57] + "..." : value)}\"");
                    }
                }

                // 检查是否有替换字符（常见乱码标志）
                bool hasSuspicious = value.Contains('\uFFFD') || // Unicode 替换字符
                                     value.Contains('\0');        // Null 字符
                if (hasSuspicious)
                    suspiciousEntries++;
            }

            sb.AppendLine($"    包含中文的条目数: {chineseEntries}");
            sb.AppendLine($"    疑似乱码的条目数: {suspiciousEntries}");
            if (chineseSamples.Count > 0)
            {
                sb.AppendLine("    中文示例:");
                foreach (var sample in chineseSamples)
                    sb.AppendLine($"      {sample}");
            }

            result.Passed = chineseEntries > 0 && suspiciousEntries == 0;
            result.Details = sb.ToString().TrimEnd();

            if (chineseEntries == 0)
                result.ErrorMessage = "未检测到中文字符（该文件可能不含中文，可忽略此项）";
            else if (suspiciousEntries > 0)
                result.ErrorMessage = $"检测到 {suspiciousEntries} 条疑似乱码条目";
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// TC-010: 大量条目测试
    /// </summary>
    public static TestCaseResult TC010_LargeStringTable(string filePath, EngineVersion version,
        int? expectedCount = null)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-010",
            Description = "大量 StringTable 条目测试",
            SampleFile = Path.GetFileName(filePath)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var asset = new UAsset(filePath, version);
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;

            int totalCount = 0;
            foreach (var export in asset.Exports)
            {
                if (export is StringTableExport st && st.Table != null)
                {
                    totalCount = st.Table.Count;
                    break;
                }
            }

            result.Details = $"解析耗时: {sw.ElapsedMilliseconds}ms, 条目数: {totalCount}";

            if (expectedCount.HasValue)
            {
                result.Passed = totalCount == expectedCount.Value;
                result.Details += $", 期望: {expectedCount.Value}";
            }
            else
            {
                result.Passed = totalCount > 0;
            }

            if (!result.Passed)
                result.ErrorMessage = "条目数不匹配或为 0";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }
}