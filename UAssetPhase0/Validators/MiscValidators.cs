using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetPhase0.Models;

namespace UAssetPhase0.Validators;

/// <summary>
/// TC-011: 引擎版本参数测试 — 测试不同 EngineVersion 枚举值的解析结果
/// </summary>
public static class EngineVersionValidator
{
    /// <summary>
    /// 对同一个文件尝试多个引擎版本，找出最佳匹配
    /// </summary>
    public static TestCaseResult TC011_VersionTest(string filePath)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-011",
            Description = "引擎版本参数测试",
            SampleFile = Path.GetFileName(filePath)
        };

        // 测试 UE5 相关的版本号
        var versionsToTest = new[]
        {
            EngineVersion.VER_UE5_0,
            EngineVersion.VER_UE5_1,
            EngineVersion.VER_UE5_2,
            EngineVersion.VER_UE5_3,
            EngineVersion.VER_UE5_4,
        };

        var sb = new System.Text.StringBuilder();
        var successVersions = new List<string>();

        foreach (var version in versionsToTest)
        {
            try
            {
                var asset = new UAsset(filePath, version);
                bool hasExports = asset.Exports.Count > 0;
                string status = hasExports ? "✓" : "⚠️(Exports=0)";
                sb.AppendLine($"    {status} {version}: Exports={asset.Exports.Count}");
                if (hasExports)
                    successVersions.Add(version.ToString());
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    ✗ {version}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        result.Passed = successVersions.Count > 0;
        result.Details = sb.ToString().TrimEnd();

        if (successVersions.Count > 0)
            result.Details = $"成功的版本: {string.Join(", ", successVersions)}\n{result.Details}";
        else
            result.ErrorMessage = "所有版本均无法正确解析";

        return result;
    }
}

/// <summary>
/// TC-012: 容错测试 — 验证对损坏/异常文件的处理
/// </summary>
public static class ErrorHandlingValidator
{
    public static TestCaseResult TC012_CorruptedFile(string testDir, EngineVersion version)
    {
        var result = new TestCaseResult
        {
            TestCaseId = "TC-012",
            Description = "损坏文件容错测试",
            SampleFile = "各种异常文件"
        };

        var sb = new System.Text.StringBuilder();
        int testsRun = 0;
        int testsPassed = 0;

        // 测试1: 空文件
        string emptyFile = Path.Combine(testDir, "_test_empty.uasset");
        try
        {
            File.WriteAllBytes(emptyFile, Array.Empty<byte>());
            testsRun++;
            try
            {
                var asset = new UAsset(emptyFile, version);
                sb.AppendLine("    ⚠️ 空文件: 未抛异常（意外）");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    ✓ 空文件: 正确抛出 {ex.GetType().Name}");
                testsPassed++;
            }
        }
        finally
        {
            if (File.Exists(emptyFile)) File.Delete(emptyFile);
        }

        // 测试2: 随机二进制数据
        string randomFile = Path.Combine(testDir, "_test_random.uasset");
        try
        {
            var random = new Random(42);
            var bytes = new byte[1024];
            random.NextBytes(bytes);
            File.WriteAllBytes(randomFile, bytes);
            testsRun++;
            try
            {
                var asset = new UAsset(randomFile, version);
                sb.AppendLine("    ⚠️ 随机数据文件: 未抛异常（意外）");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"    ✓ 随机数据文件: 正确抛出 {ex.GetType().Name}");
                testsPassed++;
            }
        }
        finally
        {
            if (File.Exists(randomFile)) File.Delete(randomFile);
        }

        // 测试3: 截断文件（拷贝一个真实 uasset 的前 100 字节）
        var realFiles = Directory.GetFiles(testDir, "*.uasset")
            .Where(f => !f.Contains("_test_"))
            .ToArray();

        if (realFiles.Length > 0)
        {
            string truncFile = Path.Combine(testDir, "_test_truncated.uasset");
            try
            {
                var sourceBytes = File.ReadAllBytes(realFiles[0]);
                int truncSize = Math.Min(100, sourceBytes.Length);
                File.WriteAllBytes(truncFile, sourceBytes.Take(truncSize).ToArray());
                testsRun++;
                try
                {
                    var asset = new UAsset(truncFile, version);
                    sb.AppendLine("    ⚠️ 截断文件: 未抛异常（意外）");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"    ✓ 截断文件: 正确抛出 {ex.GetType().Name}");
                    testsPassed++;
                }
            }
            finally
            {
                if (File.Exists(truncFile)) File.Delete(truncFile);
            }
        }

        // 测试4: 不存在的文件
        testsRun++;
        try
        {
            var asset = new UAsset(Path.Combine(testDir, "nonexistent_file.uasset"), version);
            sb.AppendLine("    ⚠️ 不存在的文件: 未抛异常（意外）");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"    ✓ 不存在的文件: 正确抛出 {ex.GetType().Name}");
            testsPassed++;
        }

        result.Passed = testsPassed == testsRun;
        result.Details = $"通过: {testsPassed}/{testsRun}\n{sb.ToString().TrimEnd()}";

        if (testsPassed < testsRun)
            result.ErrorMessage = $"{testsRun - testsPassed} 个容错测试未按预期处理";

        return result;
    }
}
