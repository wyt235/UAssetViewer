using System.Diagnostics;
using System.Text;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetPhase0.Models;

namespace UAssetPhase0.Validators;

/// <summary>
/// DataTable 验证器：验证 UAssetAPI 对 DataTable 类型 .uasset 的解析能力
/// </summary>
public static class DataTableValidator
{
    /// <summary>
    /// TC-001: 基础加载能力 — 验证 UAsset 能否无异常加载文件
    /// </summary>
    public static (TestCaseResult Result, UAsset? Asset) TC001_BasicLoad(string filePath, EngineVersion version)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-001",
            Description = "基础加载能力",
            SampleFile = Path.GetFileName(filePath)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var asset = new UAsset(filePath, version);
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            result.Passed = asset.Exports.Count > 0;
            result.Details = $"Exports 数量: {asset.Exports.Count}, " +
                             $"NameMap 条目数: {asset.GetNameMapIndexList().Count}, " +
                             $"文件版本: {asset.ObjectVersionUE5}";

            if (!result.Passed)
                result.ErrorMessage = "Exports 列表为空";

            return (result, asset);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            return (result, null);
        }
    }

    /// <summary>
    /// TC-002: DataTable 类型识别 — 验证 Exports 中是否包含 DataTableExport
    /// </summary>
    public static (TestCaseResult Result, DataTableExport? Export) TC002_TypeIdentification(UAsset asset, string filePath)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-002",
            Description = "DataTable 类型识别",
            SampleFile = Path.GetFileName(filePath)
        };

        var sb = new StringBuilder();
        DataTableExport? dtExport = null;

        for (int i = 0; i < asset.Exports.Count; i++)
        {
            var export = asset.Exports[i];
            sb.AppendLine($"    Export[{i}]: {export.GetType().Name} (ClassIndex: {export.ClassIndex})");

            if (export is DataTableExport dt)
            {
                dtExport = dt;
            }
        }

        result.Passed = dtExport != null;
        result.Details = sb.ToString().TrimEnd();

        if (!result.Passed)
            result.ErrorMessage = "未找到 DataTableExport 类型的 Export";

        return (result, dtExport);
    }

    /// <summary>
    /// TC-003: DataTable 行数校验 — 检查解析出的行数
    /// </summary>
    public static TestCaseResult TC003_RowCount(DataTableExport dtExport, string filePath, int? expectedRowCount = null)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-003",
            Description = "DataTable 行数校验",
            SampleFile = Path.GetFileName(filePath)
        };

        try
        {
            var table = dtExport.Table;
            int actualCount = table.Data.Count;

            result.Details = $"实际行数: {actualCount}";

            if (expectedRowCount.HasValue)
            {
                result.Passed = actualCount == expectedRowCount.Value;
                result.Details += $", 期望行数: {expectedRowCount.Value}";
                if (!result.Passed)
                    result.ErrorMessage = $"行数不匹配: 期望 {expectedRowCount.Value}, 实际 {actualCount}";
            }
            else
            {
                result.Passed = actualCount > 0;
                result.Details += " (未提供期望值，仅检查非空)";
                if (!result.Passed)
                    result.ErrorMessage = "DataTable 行数为 0";
            }
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// TC-004: DataTable 字段值详细输出 — 输出前 N 行数据用于人工对比
    /// </summary>
    public static TestCaseResult TC004_FieldValues(DataTableExport dtExport, string filePath, int maxRows = 5)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-004",
            Description = "DataTable 字段值校验（人工对比）",
            SampleFile = Path.GetFileName(filePath)
        };

        try
        {
            var table = dtExport.Table;
            var sb = new StringBuilder();
            int rowsToShow = Math.Min(maxRows, table.Data.Count);

            // 输出表头信息（从第一行的属性名提取列名）
            if (table.Data.Count > 0)
            {
                var firstRow = table.Data[0];
                sb.Append("    [列名] RowName");
                foreach (var prop in firstRow.Value)
                {
                    sb.Append($" | {prop.Name}({prop.PropertyType})");
                }
                sb.AppendLine();
                sb.AppendLine($"    {"".PadRight(80, '-')}");
            }

            // 输出数据行
            for (int i = 0; i < rowsToShow; i++)
            {
                var row = table.Data[i];
                sb.Append($"    [Row {i}] {row.Name}");
                foreach (var prop in row.Value)
                {
                    string valueStr = FormatPropertyValue(prop);
                    sb.Append($" | {valueStr}");
                }
                sb.AppendLine();
            }

            if (table.Data.Count > rowsToShow)
                sb.AppendLine($"    ... 还有 {table.Data.Count - rowsToShow} 行未显示");

            result.Passed = true; // 人工对比，只要能输出就算通过
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
    /// TC-005: 复杂结构体解析 — 检查是否存在特殊属性类型并尝试读取
    /// </summary>
    public static TestCaseResult TC005_ComplexStruct(DataTableExport dtExport, string filePath)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-005",
            Description = "复杂结构体字段类型解析",
            SampleFile = Path.GetFileName(filePath)
        };

        try
        {
            var table = dtExport.Table;
            var typeStats = new Dictionary<string, int>();
            var failedTypes = new List<string>();
            int totalProps = 0;
            int successProps = 0;

            foreach (var row in table.Data)
            {
                foreach (var prop in row.Value)
                {
                    totalProps++;
                    string typeName = prop.PropertyType?.ToString() ?? "null";

                    if (!typeStats.ContainsKey(typeName))
                        typeStats[typeName] = 0;
                    typeStats[typeName]++;

                    // 尝试读取值，检查是否会出错
                    try
                    {
                        _ = FormatPropertyValue(prop);
                        successProps++;
                    }
                    catch
                    {
                        if (!failedTypes.Contains(typeName))
                            failedTypes.Add(typeName);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"    字段总数: {totalProps}, 成功解析: {successProps}, 失败: {totalProps - successProps}");
            sb.AppendLine("    字段类型分布:");
            foreach (var kvp in typeStats.OrderByDescending(x => x.Value))
            {
                string status = failedTypes.Contains(kvp.Key) ? "⚠️" : "✓";
                sb.AppendLine($"      {status} {kvp.Key}: {kvp.Value} 个");
            }

            if (failedTypes.Count > 0)
            {
                sb.AppendLine($"    无法解析的类型: {string.Join(", ", failedTypes)}");
            }

            result.Passed = failedTypes.Count == 0;
            result.Details = sb.ToString().TrimEnd();

            if (failedTypes.Count > 0)
                result.ErrorMessage = $"有 {failedTypes.Count} 种字段类型解析失败";
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// TC-006: 大表性能测试
    /// </summary>
    public static TestCaseResult TC006_Performance(string filePath, EngineVersion version, int maxAcceptableMs = 5000)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-006",
            Description = "大表性能测试",
            SampleFile = Path.GetFileName(filePath)
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var asset = new UAsset(filePath, version);
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;

            int rowCount = 0;
            foreach (var export in asset.Exports)
            {
                if (export is DataTableExport dt)
                {
                    rowCount = dt.Table.Data.Count;
                    break;
                }
            }

            result.Passed = sw.ElapsedMilliseconds <= maxAcceptableMs;
            result.Details = $"解析耗时: {sw.ElapsedMilliseconds}ms, 行数: {rowCount}, 阈值: {maxAcceptableMs}ms";

            if (!result.Passed)
                result.ErrorMessage = $"解析耗时 {sw.ElapsedMilliseconds}ms 超过阈值 {maxAcceptableMs}ms";
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

    /// <summary>
    /// 格式化属性值为可读字符串
    /// </summary>
    public static string FormatPropertyValue(PropertyData prop)
    {
        if (prop == null) return "<null>";

        try
        {
            // 处理结构体属性
            if (prop is StructPropertyData structProp)
            {
                if (structProp.Value != null && structProp.Value.Count > 0)
                {
                    var innerValues = structProp.Value
                        .Take(3)
                        .Select(p => $"{p.Name}={FormatPropertyValue(p)}")
                        .ToList();
                    string suffix = structProp.Value.Count > 3 ? ",..." : "";
                    return $"{{{string.Join(",", innerValues)}{suffix}}}";
                }
                return "{empty struct}";
            }

            // 处理数组属性
            if (prop is ArrayPropertyData arrayProp)
            {
                int count = arrayProp.Value?.Length ?? 0;
                if (count == 0) return "[]";
                var items = arrayProp.Value!
                    .Take(3)
                    .Select(p => FormatPropertyValue(p))
                    .ToList();
                string suffix = count > 3 ? $",...({count} total)" : "";
                return $"[{string.Join(",", items)}{suffix}]";
            }

            // 处理 Map 属性
            if (prop is MapPropertyData mapProp)
            {
                int count = mapProp.Value?.Count ?? 0;
                return $"Map({count} entries)";
            }

            // 处理 Set 属性
            if (prop is SetPropertyData setProp)
            {
                int count = setProp.Value?.Length ?? 0;
                return $"Set({count} items)";
            }

            // 处理软引用
            if (prop is SoftObjectPropertyData softObj)
            {
                return $"SoftObj({softObj.Value.AssetPath.AssetName})";
            }

            // 处理文本（FText）
            if (prop is TextPropertyData textProp)
            {
                return $"\"{textProp.Value?.Value ?? "<null>"}\"";
            }

            // 通用 RawValue
            var rawValue = prop.RawValue;
            if (rawValue == null) return "<null>";

            string str = rawValue.ToString() ?? "<null>";
            // 截断过长的值
            if (str.Length > 80)
                str = str[..77] + "...";
            return str;
        }
        catch (Exception ex)
        {
            return $"<ERROR: {ex.Message}>";
        }
    }
}
