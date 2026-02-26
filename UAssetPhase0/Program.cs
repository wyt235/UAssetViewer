using System.Text;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetPhase0;
using UAssetPhase0.Models;
using UAssetPhase0.Validators;

// ============================================================================
// Phase 0: UAssetAPI 可行性验证 — 主入口
// ============================================================================
// 用法:
//   dotnet run                                  → 使用默认 TestAssets 目录
//   dotnet run -- <测试资产目录路径>              → 指定自定义目录
//   dotnet run -- <目录> --version UE5_2         → 指定引擎版本
//
// 准备工作:
//   1. 将 DataTable 的 .uasset + .uexp 文件放到 TestAssets/ 目录
//   2. 将 StringTable 的 .uasset + .uexp 文件放到 TestAssets/ 目录
//   3. 运行本程序
// ============================================================================

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// --- 解析命令行参数 ---
string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestAssets");
EngineVersion engineVersion = EngineVersion.VER_UE5_3;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--version" && i + 1 < args.Length)
    {
        engineVersion = args[i + 1].ToUpper() switch
        {
            "UE5_0" or "VER_UE5_0" => EngineVersion.VER_UE5_0,
            "UE5_1" or "VER_UE5_1" => EngineVersion.VER_UE5_1,
            "UE5_2" or "VER_UE5_2" => EngineVersion.VER_UE5_2,
            "UE5_3" or "VER_UE5_3" => EngineVersion.VER_UE5_3,
            "UE5_4" or "VER_UE5_4" => EngineVersion.VER_UE5_4,
            _ => EngineVersion.VER_UE5_3
        };
        i++;
    }
    else if (!args[i].StartsWith("--"))
    {
        testDir = args[i];
    }
}

// --- 确保测试目录存在 ---
if (!Directory.Exists(testDir))
{
    // 如果默认目录不存在，尝试项目目录下的 TestAssets
    string altDir = Path.Combine(Directory.GetCurrentDirectory(), "TestAssets");
    if (Directory.Exists(altDir))
    {
        testDir = altDir;
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"错误: 测试目录不存在: {testDir}");
        Console.WriteLine();
        Console.WriteLine("请按以下步骤准备测试文件:");
        Console.WriteLine($"  1. 创建目录: {testDir}");
        Console.WriteLine("  2. 将 DataTable 和 StringTable 的 .uasset + .uexp 文件放入该目录");
        Console.WriteLine("  3. 重新运行本程序");
        Console.ResetColor();
        return;
    }
}

// --- 扫描测试文件 ---
var uassetFiles = Directory.GetFiles(testDir, "*.uasset")
    .Where(f => !Path.GetFileName(f).StartsWith("_test_")) // 排除容错测试产生的临时文件
    .OrderBy(f => f)
    .ToArray();

if (uassetFiles.Length == 0)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"警告: 在 {testDir} 中未找到任何 .uasset 文件");
    Console.WriteLine("请将测试用的 .uasset 文件（连同 .uexp）放入该目录后重试");
    Console.ResetColor();
    return;
}

// --- 开始验证 ---
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║     Phase 0: UAssetAPI 可行性验证                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  测试目录:   {Path.GetFullPath(testDir)}");
Console.WriteLine($"  引擎版本:   {engineVersion}");
Console.WriteLine($"  文件数量:   {uassetFiles.Length} 个 .uasset");
Console.WriteLine();

// 检查 .uexp 配对情况
foreach (var file in uassetFiles)
{
    string expFile = Path.ChangeExtension(file, ".uexp");
    bool hasExp = File.Exists(expFile);
    string icon = hasExp ? "✓" : "⚠️";
    Console.WriteLine($"  {icon} {Path.GetFileName(file)}{(hasExp ? "" : " (缺少 .uexp)")}");
}
Console.WriteLine();

var allResults = new List<TestCaseResult>();

// ============================================================================
// 第一阶段: DataTable 验证 (TC-001 ~ TC-006)
// ============================================================================
Console.WriteLine("━━━ 第一阶段: DataTable 验证 ━━━");
Console.WriteLine();

// 自动识别: 尝试加载每个文件，找出 DataTable 类型的文件
var dataTableFiles = new List<string>();
var stringTableFiles = new List<string>();
var unknownFiles = new List<string>();

foreach (var file in uassetFiles)
{
    try
    {
        var asset = new UAssetAPI.UAsset(file, engineVersion);
        bool isDT = asset.Exports.Any(e => e is DataTableExport);
        bool isST = asset.Exports.Any(e => e is StringTableExport);

        if (isDT) dataTableFiles.Add(file);
        else if (isST) stringTableFiles.Add(file);
        else unknownFiles.Add(file);
    }
    catch
    {
        unknownFiles.Add(file);
    }
}

Console.WriteLine($"  扫描结果: DataTable={dataTableFiles.Count}, StringTable={stringTableFiles.Count}, 其他/失败={unknownFiles.Count}");
Console.WriteLine();

// 对每个 DataTable 文件执行验证
foreach (var dtFile in dataTableFiles)
{
    Console.WriteLine($"┌── DataTable: {Path.GetFileName(dtFile)} ──┐");

    // TC-001: 基础加载
    var (r001, asset) = DataTableValidator.TC001_BasicLoad(dtFile, engineVersion);
    ReportGenerator.PrintResult(r001);
    allResults.Add(r001);

    if (asset != null)
    {
        // TC-002: 类型识别
        var (r002, dtExport) = DataTableValidator.TC002_TypeIdentification(asset, dtFile);
        ReportGenerator.PrintResult(r002);
        allResults.Add(r002);

        if (dtExport != null)
        {
            // TC-003: 行数校验
            var r003 = DataTableValidator.TC003_RowCount(dtExport, dtFile);
            ReportGenerator.PrintResult(r003);
            allResults.Add(r003);

            // TC-004: 字段值输出
            var r004 = DataTableValidator.TC004_FieldValues(dtExport, dtFile);
            ReportGenerator.PrintResult(r004);
            allResults.Add(r004);

            // TC-005: 复杂结构体
            var r005 = DataTableValidator.TC005_ComplexStruct(dtExport, dtFile);
            ReportGenerator.PrintResult(r005);
            allResults.Add(r005);
        }
    }

    Console.WriteLine($"└{"".PadRight(50, '─')}┘");
    Console.WriteLine();
}

// TC-006: 性能测试（选最大的 DataTable 文件）
if (dataTableFiles.Count > 0)
{
    var largestDT = dataTableFiles.OrderByDescending(f => new FileInfo(f).Length).First();
    Console.WriteLine($"┌── TC-006 性能测试: {Path.GetFileName(largestDT)} ({new FileInfo(largestDT).Length / 1024}KB) ──┐");
    var r006 = DataTableValidator.TC006_Performance(largestDT, engineVersion);
    ReportGenerator.PrintResult(r006);
    allResults.Add(r006);
    Console.WriteLine($"└{"".PadRight(50, '─')}┘");
    Console.WriteLine();
}

// ============================================================================
// 第二阶段: StringTable 验证 (TC-007 ~ TC-010)
// ============================================================================
Console.WriteLine("━━━ 第二阶段: StringTable 验证 ━━━");
Console.WriteLine();

foreach (var stFile in stringTableFiles)
{
    Console.WriteLine($"┌── StringTable: {Path.GetFileName(stFile)} ──┐");

    // TC-007: 类型识别
    var (r007, stAsset, stExport) = StringTableValidator.TC007_TypeIdentification(stFile, engineVersion);
    ReportGenerator.PrintResult(r007);
    allResults.Add(r007);

    if (stExport != null)
    {
        // TC-008: 内容读取
        var r008 = StringTableValidator.TC008_ContentRead(stExport, stFile);
        ReportGenerator.PrintResult(r008);
        allResults.Add(r008);

        // TC-009: 中文支持
        var r009 = StringTableValidator.TC009_ChineseSupport(stExport, stFile);
        ReportGenerator.PrintResult(r009);
        allResults.Add(r009);
    }

    Console.WriteLine($"└{"".PadRight(50, '─')}┘");
    Console.WriteLine();
}

// TC-010: 大量条目（选最大的 StringTable）
if (stringTableFiles.Count > 0)
{
    var largestST = stringTableFiles.OrderByDescending(f => new FileInfo(f).Length).First();
    Console.WriteLine($"┌── TC-010 大量条目: {Path.GetFileName(largestST)} ({new FileInfo(largestST).Length / 1024}KB) ──┐");
    var r010 = StringTableValidator.TC010_LargeStringTable(largestST, engineVersion);
    ReportGenerator.PrintResult(r010);
    allResults.Add(r010);
    Console.WriteLine($"└{"".PadRight(50, '─')}┘");
    Console.WriteLine();
}

// ============================================================================
// 第三阶段: 补充验证 (TC-011, TC-012)
// ============================================================================
Console.WriteLine("━━━ 第三阶段: 补充验证 ━━━");
Console.WriteLine();

// TC-011: 引擎版本测试（用第一个成功的文件）
if (uassetFiles.Length > 0)
{
    string testFile = dataTableFiles.FirstOrDefault() ?? stringTableFiles.FirstOrDefault() ?? uassetFiles[0];
    Console.WriteLine($"┌── TC-011 引擎版本: {Path.GetFileName(testFile)} ──┐");
    var r011 = EngineVersionValidator.TC011_VersionTest(testFile);
    ReportGenerator.PrintResult(r011);
    allResults.Add(r011);
    Console.WriteLine($"└{"".PadRight(50, '─')}┘");
    Console.WriteLine();
}

// TC-012: 容错测试
Console.WriteLine($"┌── TC-012 容错测试 ──┐");
var r012 = ErrorHandlingValidator.TC012_CorruptedFile(testDir, engineVersion);
ReportGenerator.PrintResult(r012);
allResults.Add(r012);
Console.WriteLine($"└{"".PadRight(50, '─')}┘");
Console.WriteLine();

// ============================================================================
// 输出总结 & 生成报告
// ============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
int totalPassed = allResults.Count(r => r.Passed);
int totalFailed = allResults.Count - totalPassed;

Console.ForegroundColor = totalFailed == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
Console.WriteLine($"  总计: {allResults.Count} 项  |  通过: {totalPassed}  |  未通过: {totalFailed}");
Console.ResetColor();

if (unknownFiles.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"\n  未识别的文件 ({unknownFiles.Count} 个):");
    foreach (var f in unknownFiles)
        Console.WriteLine($"    - {Path.GetFileName(f)}");
    Console.ResetColor();
}

// 生成 Markdown 报告
string reportDir = Path.Combine(testDir, "..", "Results");
string reportPath = Path.Combine(reportDir, $"Phase0_验证报告_{DateTime.Now:yyyyMMdd_HHmmss}.md");
ReportGenerator.GenerateReport(allResults, reportPath);

Console.WriteLine();
Console.WriteLine("按任意键退出...");
Console.ReadKey(true);
