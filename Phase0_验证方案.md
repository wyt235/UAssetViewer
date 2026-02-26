# Phase 0 — UAssetAPI 可行性验证方案

> **目标**：用最小成本验证 UAssetAPI 能否正确解析项目中魔改引擎产出的 DataTable 和 StringTable  
> **预计耗时**：1~2 天  
> **产出物**：验证结论报告 + 可运行的控制台原型程序  
> **判定标准**：3 个 DataTable + 2 个 StringTable 均可正确解析 → ✅ 通过

---

## 一、准备工作

### 1.1 环境准备

| 项目 | 要求 |
|------|------|
| .NET SDK | 8.0 或更高版本 |
| IDE | Visual Studio 2022 / Rider / VS Code + C# DevKit |
| NuGet 包 | `UAssetAPI`（最新稳定版） |
| 操作系统 | Windows 10/11 |

### 1.2 测试样本准备

需要从项目 `Content` 目录中挑选以下测试文件（`.uasset` + 对应 `.uexp`）：

| 编号 | 类型 | 选取标准 | 文件名示例 |
|------|------|---------|-----------|
| S-01 | DataTable | **简单结构体**：行结构体仅包含基础类型（int, float, FString, FName, bool） | `DT_SimpleConfig.uasset` |
| S-02 | DataTable | **复杂结构体**：包含 Enum、TArray、嵌套 Struct、TSoftObjectPtr 等类型 | `DT_ItemData.uasset` |
| S-03 | DataTable | **大规模数据**：行数 > 500 的大表，验证性能和完整性 | `DT_Localization.uasset` |
| S-04 | StringTable | **标准 StringTable**：常见的本地化字符串表 | `ST_UI_ZH.uasset` |
| S-05 | StringTable | **大规模 StringTable**：条目数 > 200 | `ST_Dialog.uasset` |

> ⚠️ **注意事项**：  
> - 每个 `.uasset` 需要连同其同名 `.uexp` 文件一起拷贝  
> - 建议将测试样本统一放到项目中的 `TestAssets/` 目录下  
> - 同时在 UE 编辑器中打开这些资产，**截图或导出 CSV 作为对照基准**

### 1.3 对照基准数据获取

在 UE 编辑器中为每个测试样本获取基准数据：

1. **DataTable**：在编辑器中打开 → 右键 → `Export as CSV`，保存到 `TestAssets/Baseline/` 目录
2. **StringTable**：在编辑器中打开 → 手动记录（或截图）Key-Value 对数量和前 10 条内容
3. 记录每个测试文件的 **行数/条目数** 作为数量校验基准

---

## 二、验证项目搭建

### 2.1 项目创建

```bash
# 创建控制台项目
dotnet new console -n UAssetPhase0 -f net8.0
cd UAssetPhase0

# 安装 UAssetAPI
dotnet add package UAssetAPI
```

### 2.2 项目结构

```
UAssetPhase0/
├── Program.cs              # 主入口，执行所有验证用例
├── Validators/
│   ├── DataTableValidator.cs   # DataTable 解析与验证逻辑
│   └── StringTableValidator.cs # StringTable 解析与验证逻辑
├── TestAssets/                 # 测试用 uasset 文件（git忽略）
│   ├── DT_SimpleConfig.uasset
│   ├── DT_SimpleConfig.uexp
│   ├── ...
│   └── Baseline/               # UE编辑器导出的对照数据
│       ├── DT_SimpleConfig.csv
│       └── ...
├── Results/                    # 验证结果输出目录
└── UAssetPhase0.csproj
```

---

## 三、验证用例设计

### 3.1 验证用例清单

| 用例编号 | 验证目标 | 输入样本 | 期望结果 | 优先级 |
|---------|---------|---------|---------|--------|
| **TC-001** | 基础加载能力 | S-01 | `new UAsset()` 不抛异常，Exports 列表非空 | 🔴 P0 |
| **TC-002** | DataTable 类型识别 | S-01 | Exports 中存在 `DataTableExport` 类型实例 | 🔴 P0 |
| **TC-003** | DataTable 行数校验 | S-01 | 解析出的行数 == UE 编辑器中的行数 | 🔴 P0 |
| **TC-004** | DataTable 字段值校验 | S-01 | 前 5 行的每个字段值 == CSV 对照数据 | 🔴 P0 |
| **TC-005** | 复杂结构体解析 | S-02 | Enum 字段、数组字段、软引用字段均有有效值输出 | 🔴 P0 |
| **TC-006** | 大表性能 | S-03 | 解析耗时 < 5 秒，行数与基准一致 | 🟡 P1 |
| **TC-007** | StringTable 类型识别 | S-04 | Exports 中存在 `StringTableExport` 类型实例 | 🔴 P0 |
| **TC-008** | StringTable 内容读取 | S-04 | Key-Value 对数量与基准一致，前 10 条内容匹配 | 🔴 P0 |
| **TC-009** | StringTable 中文支持 | S-04 | 中文字符串正确显示，无乱码 | 🔴 P0 |
| **TC-010** | 大量 StringTable 条目 | S-05 | 条目数与基准一致 | 🟡 P1 |
| **TC-011** | 引擎版本参数测试 | S-01 | 分别使用 `VER_UE5_3` 和相邻版本号，确认哪个能正确解析 | 🟡 P1 |
| **TC-012** | 损坏文件容错 | 空文件/截断文件 | 抛出可捕获的异常，不崩溃 | 🟡 P1 |

### 3.2 核心验证代码骨架

```csharp
// Program.cs — 主流程
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using System.Diagnostics;

class Program
{
    // 根据实际情况调整引擎版本
    static readonly EngineVersion UE_VERSION = EngineVersion.VER_UE5_3;
    
    static void Main(string[] args)
    {
        string testDir = args.Length > 0 ? args[0] : @".\TestAssets";
        
        Console.WriteLine("========== Phase 0: UAssetAPI 可行性验证 ==========\n");
        
        // 遍历所有 .uasset 文件
        foreach (var file in Directory.GetFiles(testDir, "*.uasset"))
        {
            Console.WriteLine($"--- 正在验证: {Path.GetFileName(file)} ---");
            ValidateAsset(file);
            Console.WriteLine();
        }
        
        Console.WriteLine("========== 验证完成 ==========");
    }
    
    static void ValidateAsset(string filePath)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // TC-001: 基础加载
            UAsset asset = new UAsset(filePath, UE_VERSION);
            sw.Stop();
            Console.WriteLine($"  [✓] TC-001 加载成功 ({sw.ElapsedMilliseconds}ms)");
            Console.WriteLine($"  [i] Exports 数量: {asset.Exports.Count}");
            
            foreach (var export in asset.Exports)
            {
                Console.WriteLine($"  [i] Export 类型: {export.GetType().Name}");
                
                // TC-002 / TC-003 / TC-004: DataTable 验证
                if (export is DataTableExport dtExport)
                {
                    Console.WriteLine($"  [✓] TC-002 识别为 DataTable");
                    var table = dtExport.Table;
                    Console.WriteLine($"  [i] TC-003 行数: {table.Data.Count}");
                    
                    // 输出前 5 行用于人工对比
                    int showRows = Math.Min(5, table.Data.Count);
                    for (int i = 0; i < showRows; i++)
                    {
                        var row = table.Data[i];
                        Console.WriteLine($"  [i] Row[{i}] Name={row.Name}, 字段数={row.Value.Count}");
                        foreach (var prop in row.Value)
                        {
                            Console.WriteLine($"       - {prop.Name} ({prop.PropertyType}): {prop.RawValue}");
                        }
                    }
                }
                
                // TC-007 / TC-008 / TC-009: StringTable 验证
                if (export is StringTableExport stExport)
                {
                    Console.WriteLine($"  [✓] TC-007 识别为 StringTable");
                    // 根据 UAssetAPI 版本，StringTable 的访问方式可能略有不同
                    // 以下为通用思路，需根据实际 API 调整
                    try
                    {
                        var tableData = stExport.Table;
                        Console.WriteLine($"  [i] TC-008 StringTable 内容已读取");
                        // 遍历输出前 10 条
                        int count = 0;
                        foreach (var kvp in tableData)
                        {
                            if (count >= 10) break;
                            Console.WriteLine($"       - Key=\"{kvp.Key}\"  Value=\"{kvp.Value}\"");
                            count++;
                        }
                        Console.WriteLine($"  [i] 总条目数: {tableData.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [!] StringTable 内容读取异常: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"  [✗] 加载失败: {ex.GetType().Name}");
            Console.WriteLine($"  [✗] 错误信息: {ex.Message}");
            Console.WriteLine($"  [✗] 堆栈(前3行):");
            var stackLines = ex.StackTrace?.Split('\n').Take(3) ?? Array.Empty<string>();
            foreach (var line in stackLines)
            {
                Console.WriteLine($"       {line.Trim()}");
            }
        }
    }
}
```

---

## 四、验证结果记录模板

验证完成后，按以下模板记录每个测试用例的结果：

### 4.1 结果汇总表

| 用例 | 样本 | 结果 | 备注 |
|------|------|------|------|
| TC-001 | S-01 | ✅/❌ | |
| TC-002 | S-01 | ✅/❌ | |
| TC-003 | S-01 | ✅/❌ | 期望行数=___, 实际行数=___ |
| TC-004 | S-01 | ✅/❌ | 不一致的字段: ___ |
| TC-005 | S-02 | ✅/❌ | 无法解析的字段类型: ___ |
| TC-006 | S-03 | ✅/❌ | 耗时=___ms |
| TC-007 | S-04 | ✅/❌ | |
| TC-008 | S-04 | ✅/❌ | 期望条目数=___, 实际条目数=___ |
| TC-009 | S-04 | ✅/❌ | 乱码内容: ___ |
| TC-010 | S-05 | ✅/❌ | |
| TC-011 | S-01 | ✅/❌ | 最佳版本号: ___ |
| TC-012 | 损坏文件 | ✅/❌ | |

### 4.2 问题记录模板

对于失败的用例，按以下格式详细记录：

```
### 问题编号: ISSUE-XXX
- **关联用例**: TC-XXX
- **测试样本**: S-XX (文件名)
- **现象描述**: 
- **错误信息**: 
- **初步分析**: 
- **是否与魔改引擎相关**: 是/否/不确定
- **可能的解决方案**: 
```

---

## 五、通过/不通过判定标准

### 5.1 通过（Go）

满足以下 **全部条件**，判定为通过，可进入 Phase 1 正式开发：

- [x] TC-001 ~ TC-004 全部通过（基础 DataTable 解析正常）
- [x] TC-005 通过，或仅个别罕见字段类型无法解析但不影响主体数据
- [x] TC-007 ~ TC-009 全部通过（基础 StringTable 解析正常）
- [x] 无因魔改引擎导致的 **系统性解析失败**（即不是所有文件都报同一个错）

### 5.2 有条件通过（Go with Caveats）

满足以下条件，可进入 Phase 1 但需在架构中预留适配层：

- [x] DataTable 和 StringTable 的主体结构可解析
- [ ] 部分字段类型解析为 Unknown / RawData（可在 UI 上标注为"未知类型"）
- [ ] 需要使用自定义 `.usmap` Mappings 才能正确解析

### 5.3 不通过（No-Go）

出现以下 **任一情况**，判定为不通过，需执行 Plan B：

- ❌ 所有 `.uasset` 均无法加载（`new UAsset()` 即抛异常）
- ❌ 可加载但 Exports 为空或无法识别出 DataTable/StringTable 类型
- ❌ 魔改引擎修改了 Package Summary / Tag 序列化格式，导致数据偏移全错

### 5.4 Plan B 备选方案

如果 Phase 0 判定为 No-Go，有以下备选路径：

| 方案 | 思路 | 优点 | 缺点 |
|------|------|------|------|
| **B-1**: UE Commandlet 导出 | 写一个 UE Commandlet 批量导出 DataTable/StringTable 为 JSON/CSV，工具读取导出文件 | 100% 兼容魔改引擎 | 依赖 UE 环境，需要定期重新导出 |
| **B-2**: 自定义 Mappings | 向 UAssetAPI 提供 `.usmap` 文件，覆盖魔改的属性/结构体定义 | 仍保持脱离 UE 的优势 | 需要深入了解魔改内容来制作 Mappings |
| **B-3**: Fork UAssetAPI | Fork UAssetAPI 源码，针对魔改部分做定制修改 | 完全可控 | 维护成本高，后续合并上游更新困难 |
| **B-4**: UnrealPak + Python | 用 UnrealPak 解包，再用 Python 脚本做二进制解析 | 灵活 | 开发量大，可靠性低 |

---

## 六、时间安排

| 日期 | 任务 | 产出 |
|------|------|------|
| **Day 1 上午** | 搭建项目、安装依赖、准备测试样本和基准数据 | 可运行的空项目 + TestAssets 目录 |
| **Day 1 下午** | 执行 TC-001 ~ TC-005（DataTable 验证） | DataTable 验证结论 |
| **Day 2 上午** | 执行 TC-007 ~ TC-010（StringTable 验证）+ TC-006（性能）+ TC-011/012 | 全部验证结论 |
| **Day 2 下午** | 编写验证结论报告，Go/No-Go 决策 | Phase 0 验证报告文档 |

---

## 七、交付物清单

- [ ] `UAssetPhase0/` — 可运行的控制台验证程序
- [ ] `TestAssets/` — 测试样本文件（含 UE 编辑器导出的对照基准）
- [ ] `Phase0_验证报告.md` — 验证结果和 Go/No-Go 结论
- [ ] 如果 No-Go：Plan B 方案选型建议
