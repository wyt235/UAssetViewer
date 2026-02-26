using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace UAssetViewer.Helpers;

/// <summary>
/// 将 UAssetAPI 的 PropertyData 转为可读字符串，用于 DataGrid 单元格显示
/// </summary>
public static class PropertyFormatter
{
    /// <summary>
    /// 将任意 PropertyData 格式化为显示字符串
    /// </summary>
    public static string Format(PropertyData? prop)
    {
        if (prop == null) return "";

        try
        {
            return prop switch
            {
                BoolPropertyData b => b.Value.ToString(),
                IntPropertyData i => i.Value.ToString(),
                FloatPropertyData f => f.Value.ToString("G"),
                DoublePropertyData d => d.Value.ToString("G"),
                Int8PropertyData i8 => i8.Value.ToString(),
                Int16PropertyData i16 => i16.Value.ToString(),
                Int64PropertyData i64 => i64.Value.ToString(),
                UInt16PropertyData u16 => u16.Value.ToString(),
                UInt32PropertyData u32 => u32.Value.ToString(),
                UInt64PropertyData u64 => u64.Value.ToString(),
                BytePropertyData byteP => FormatByte(byteP),
                StrPropertyData str => str.Value?.Value ?? "<null>",
                NamePropertyData name => name.Value?.Value?.Value ?? "<null>",
                TextPropertyData text => FormatText(text),
                EnumPropertyData enumP => enumP.Value?.Value?.Value ?? "<null>",
                SoftObjectPropertyData softObj => FormatSoftObject(softObj),
                ObjectPropertyData obj => obj.Value?.ToString() ?? "<null>",
                StructPropertyData structP => FormatStruct(structP),
                SetPropertyData setP => $"Set({setP.Value?.Length ?? 0} entries)",
                ArrayPropertyData arr => FormatArray(arr),
                MapPropertyData map => $"Map({map.Value?.Count ?? 0} entries)",
                _ => prop.RawValue?.ToString() ?? prop.ToString() ?? "<unknown>"
            };
        }
        catch
        {
            return $"<error:{prop.GetType().Name}>";
        }
    }

    private static string FormatByte(BytePropertyData byteP)
    {
        if (byteP.ByteType == BytePropertyType.Byte)
            return byteP.Value.ToString();
        return byteP.EnumValue?.Value?.Value ?? byteP.Value.ToString();
    }

    private static string FormatText(TextPropertyData text)
    {
        // TextPropertyData.Value 是 FString
        var val = text.Value?.Value;
        if (val != null) return val;

        // 尝试 CultureInvariantString
        var cis = text.CultureInvariantString?.Value;
        if (cis != null) return cis;

        return "<null>";
    }

    private static string FormatSoftObject(SoftObjectPropertyData softObj)
    {
        var assetName = softObj.Value.AssetPath.AssetName?.ToString();
        if (!string.IsNullOrEmpty(assetName))
            return assetName;
        return softObj.Value.ToString() ?? "<null>";
    }

    private static string FormatStruct(StructPropertyData structP, int depth = 0)
    {
        if (structP.Value == null || structP.Value.Count == 0)
            return "{}";

        if (depth > 1)
            return "{...}";

        // 限制显示的字段数量
        const int maxFields = 4;
        var parts = new List<string>();
        for (int i = 0; i < structP.Value.Count && i < maxFields; i++)
        {
            var child = structP.Value[i];
            string name = child.Name?.Value?.Value ?? $"[{i}]";
            string value;
            if (child is StructPropertyData nestedStruct)
                value = FormatStruct(nestedStruct, depth + 1);
            else
                value = Format(child);

            // 截断过长的值
            if (value.Length > 40)
                value = value[..37] + "...";

            parts.Add($"{name}={value}");
        }

        string result = string.Join(", ", parts);
        if (structP.Value.Count > maxFields)
            result += ", ...";

        return "{" + result + "}";
    }

    private static string FormatArray(ArrayPropertyData arr)
    {
        if (arr.Value == null || arr.Value.Length == 0)
            return "[]";

        if (arr.Value.Length <= 3)
        {
            var items = arr.Value.Select(v => Format(v));
            return "[" + string.Join(", ", items) + "]";
        }

        return $"[{arr.Value.Length} items]";
    }

    /// <summary>
    /// 获取完整的格式化内容（用于 Tooltip 或详情弹窗，不截断）
    /// </summary>
    public static string FormatFull(PropertyData? prop)
    {
        if (prop == null) return "";

        if (prop is StructPropertyData structP)
            return FormatStructFull(structP, 0);

        if (prop is ArrayPropertyData arr && arr.Value != null)
        {
            var lines = arr.Value.Select((v, i) => $"  [{i}] {FormatFull(v)}");
            return "[\n" + string.Join("\n", lines) + "\n]";
        }

        if (prop is MapPropertyData map && map.Value != null)
        {
            var lines = new List<string>();
            foreach (var kvp in map.Value)
            {
                lines.Add($"  {Format(kvp.Key)} → {Format(kvp.Value)}");
            }
            return "Map {\n" + string.Join("\n", lines) + "\n}";
        }

        return Format(prop);
    }

    private static string FormatStructFull(StructPropertyData structP, int indent)
    {
        if (structP.Value == null || structP.Value.Count == 0)
            return "{}";

        var prefix = new string(' ', (indent + 1) * 2);
        var lines = new List<string>();
        foreach (var child in structP.Value)
        {
            string name = child.Name?.Value?.Value ?? "?";
            string value = child is StructPropertyData nested
                ? FormatStructFull(nested, indent + 1)
                : FormatFull(child);
            lines.Add($"{prefix}{name} = {value}");
        }

        var closePrefix = new string(' ', indent * 2);
        return "{\n" + string.Join("\n", lines) + $"\n{closePrefix}}}";
    }
}