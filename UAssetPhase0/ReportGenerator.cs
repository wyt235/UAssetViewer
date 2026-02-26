using System.Text;
using UAssetPhase0.Models;

namespace UAssetPhase0;

/// <summary>
/// 验证报告生成器 — 将所有测试结果汇总为 Markdown 报告
/// </summary>
public static class ReportGenerator
{
    public static void GenerateReport(List<TestCaseResult> results, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Phase 0 验证报告");
        sb.AppendLine();
        sb.AppendLine($"> 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // 总览
        int total = results.Count;
        int passed = results.Count(r => r.Passed);
        int failed = total - passed;
        string overallStatus = failed == 0 ? "✅ 全部通过" : $"⚠️ {failed}/{total} 项未通过";

        sb.AppendLine("## 总览");
        sb.AppendLine();
        sb.AppendLine($"| 指标 | 值 |");
        sb.AppendLine($"|------|------|");
        sb.AppendLine($"| 总用例数 | {total} |");
        sb.AppendLine($"| 通过 | {passed} |");
        sb.AppendLine($"| 未通过 | {failed} |");
        sb.AppendLine($"| 整体状态 | {overallStatus} |");
        sb.AppendLine();

        // 结果汇总表
        sb.AppendLine("## 结果汇总");
        sb.AppendLine();
        sb.AppendLine("| 用例 | 描述 | 样本文件 | 结果 | 耗时 |");
        sb.AppendLine("|------|------|---------|------|------|");
        foreach (var r in results)
        {
            string status = r.Passed ? "✅" : "❌";
            string elapsed = r.ElapsedMs > 0 ? $"{r.ElapsedMs}ms" : "-";
            sb.AppendLine($"| {r.TestCaseId} | {r.Description} | {r.SampleFile} | {status} | {elapsed} |");
        }
        sb.AppendLine();

        // 详细结果
        sb.AppendLine("## 详细结果");
        sb.AppendLine();
        foreach (var r in results)
        {
            string status = r.Passed ? "✅ PASS" : "❌ FAIL";
            sb.AppendLine($"### {r.TestCaseId} - {r.Description} [{status}]");
            sb.AppendLine();
            sb.AppendLine($"- **样本**: {r.SampleFile}");
            if (r.ElapsedMs > 0)
                sb.AppendLine($"- **耗时**: {r.ElapsedMs}ms");
            if (!string.IsNullOrEmpty(r.ErrorMessage))
                sb.AppendLine($"- **错误**: {r.ErrorMessage}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(r.Details))
            {
                sb.AppendLine("```");
                sb.AppendLine(r.Details);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        // Go/No-Go 判定
        sb.AppendLine("## Go/No-Go 判定");
        sb.AppendLine();

        var p0Cases = results.Where(r =>
            r.TestCaseId is "TC-001" or "TC-002" or "TC-003" or "TC-004" or "TC-005"
                        or "TC-007" or "TC-008" or "TC-009").ToList();
        bool p0AllPassed = p0Cases.All(r => r.Passed);

        if (p0AllPassed)
        {
            sb.AppendLine("### ✅ Go — 建议进入 Phase 1 正式开发");
            sb.AppendLine();
            sb.AppendLine("所有 P0 级别用例均已通过，UAssetAPI 能够正确解析项目中的 DataTable 和 StringTable。");
        }
        else
        {
            var failedP0 = p0Cases.Where(r => !r.Passed).ToList();
            bool anyCanLoad = results.Any(r => r.TestCaseId == "TC-001" && r.Passed);

            if (anyCanLoad && failedP0.Count <= 2)
            {
                sb.AppendLine("### ⚠️ Go with Caveats — 有条件通过");
                sb.AppendLine();
                sb.AppendLine("基础加载能力正常，但以下用例未通过，需在 Phase 1 中预留适配层：");
            }
            else
            {
                sb.AppendLine("### ❌ No-Go — 建议执行 Plan B");
                sb.AppendLine();
                sb.AppendLine("以下关键用例未通过：");
            }

            foreach (var f in failedP0)
            {
                sb.AppendLine($"- {f.TestCaseId}: {f.Description} — {f.ErrorMessage}");
            }
        }

        sb.AppendLine();

        // 写入文件
        string dir = Path.GetDirectoryName(outputPath) ?? ".";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"\n📄 验证报告已生成: {Path.GetFullPath(outputPath)}");
    }

    /// <summary>
    /// 在控制台实时输出单条结果
    /// </summary>
    public static void PrintResult(TestCaseResult result)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = result.Passed ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(result.ToString());
        Console.ForegroundColor = originalColor;
    }
}
