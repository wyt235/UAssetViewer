# Phase 1：UAsset 查看工具 — 开发计划

> 创建时间: 2026-02-26  
> Phase 0 验证结果: ✅ 全部通过 (15/15)  
> 状态: 📋 待开发

---

## 一、项目概述

### 1.1 目标
开发一个独立运行的 Windows 桌面工具，用于**不启动 UE 编辑器**的情况下查看项目中 DataTable 和 StringTable 资产的内容。

### 1.2 核心约束
| 项目 | 决策 |
|------|------|
| 最终形态 | 独立 .exe 桌面应用 |
| 运行环境 | Windows，.NET 9（兼容 .NET 8） |
| UI 框架 | **WPF**（生态成熟，DataGrid 控件完善，打包简单） |
| 解析引擎 | UAssetAPI 源码引用（已验证通过） |
| 引擎版本 | UE 5.3.2（魔改引擎，Phase 0 已确认兼容） |
| 支持类型 | 仅 DataTable + StringTable |
| 扩展性 | 暂不考虑其他资产类型，后续有需求再议 |
| 使用者 | 初期个人使用，最终可能推向全团队 |
| 文件加载 | 选择一个目录，工具自动扫描所有 DataTable/StringTable |

---

## 二、功能清单

### 2.1 P0 — 必须实现（MVP）

| 编号 | 功能 | 说明 |
|------|------|------|
| F-001 | 目录选择 | 点击按钮选择一个目录（Content 目录或任意包含 .uasset 的目录） |
| F-002 | 自动扫描 | 递归扫描目录下所有 .uasset 文件，自动识别 DataTable / StringTable / 其他 |
| F-003 | 文件列表 | 左侧面板展示扫描结果，分类显示（DataTable / StringTable），显示文件名和行/条目数 |
| F-004 | DataTable 查看 | 选中 DataTable 文件后，右侧以**表格形式**展示内容（列 = 字段名，行 = RowName） |
| F-005 | StringTable 查看 | 选中 StringTable 文件后，右侧以**两列表格**展示（Key / Value） |
| F-006 | 中文显示 | 正确显示中文字符，无乱码（Phase 0 已验证） |
| F-007 | 错误提示 | 解析失败时显示友好的错误信息，不崩溃 |

### 2.2 P1 — 建议实现（体验优化）

| 编号 | 功能 | 说明 |
|------|------|------|
| F-008 | 搜索/过滤 | 支持按 RowName、字段值、Key/Value 搜索，快速定位 |
| F-009 | 列排序 | 点击表头排序 |
| F-010 | 记住上次目录 | 下次启动自动记住上次打开的目录路径 |
| F-011 | 扫描进度 | 大量文件时显示扫描进度条 |
| F-012 | 复制单元格 | 右键复制单元格内容 |

### 2.3 P2 — 可选（后续迭代）

| 编号 | 功能 | 说明 |
|------|------|------|
| F-013 | 导出 CSV/JSON | 将当前表导出为 CSV 或 JSON |
| F-014 | 多标签页 | 同时打开多个文件 |
| F-015 | 文件对比 | 两个版本的同名表差异高亮 |
| F-016 | 批量导出 | 一键导出整个目录所有表 |

---

## 三、技术架构

### 3.1 项目结构

```
UAssetViewer/                          ← 新建 WPF 项目
├── UAssetViewer.sln
├── UAssetViewer/
│   ├── UAssetViewer.csproj            ← WPF 应用，引用 UAssetAPI
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs          ← 主窗口
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           ← 主视图模型（MVVM）
│   │   ├── FileListViewModel.cs       ← 文件列表逻辑
│   │   └── TableViewModel.cs          ← 表格数据逻辑
│   ├── Views/
│   │   ├── FileListPanel.xaml         ← 左侧文件树
│   │   ├── DataTableView.xaml         ← DataTable 表格视图
│   │   └── StringTableView.xaml       ← StringTable 表格视图
│   ├── Services/
│   │   ├── AssetScannerService.cs     ← 目录扫描 + 类型识别
│   │   ├── DataTableReader.cs         ← DataTable 解析 → ViewModel
│   │   └── StringTableReader.cs       ← StringTable 解析 → ViewModel
│   ├── Models/
│   │   ├── ScannedAsset.cs            ← 扫描结果模型
│   │   └── TableDisplayData.cs        ← 表格展示数据模型
│   └── Helpers/
│       ├── PropertyFormatter.cs       ← 属性值格式化（复用 Phase 0 逻辑）
│       └── RelayCommand.cs            ← MVVM 命令基类
└── UAssetAPI/                         ← 已有，源码引用
    └── UAssetAPI/
        └── UAssetAPI.csproj
```

### 3.2 核心设计

#### 解析层（Service）
- 复用 Phase 0 验证过的解析逻辑
- `AssetScannerService`：递归扫描目录，尝试加载每个 .uasset，分类为 DataTable / StringTable / Unknown
- `DataTableReader`：将 `DataTableExport` 转为 `List<Dictionary<string, string>>` 用于 DataGrid 绑定
- `StringTableReader`：将 `FStringTable` (TMap) 转为 `List<KeyValuePair<string, string>>`

#### UI 层
- **布局**：左右分栏（Left: 文件列表 200px，Right: 表格内容，可拖拽调整）
- **DataTable 展示**：WPF DataGrid，动态生成列（因为不同 DataTable 列数不同）
- **StringTable 展示**：WPF DataGrid，固定两列（Key, Value）
- **MVVM 模式**：ViewModel 驱动，方便后续扩展和测试

#### 数据流
```
用户选择目录
    → AssetScannerService.ScanAsync(path)
    → 返回 List<ScannedAsset>（文件名、类型、路径）
    → 绑定到左侧 FileListPanel

用户点击某个文件
    → DataTableReader.Read(path) 或 StringTableReader.Read(path)
    → 返回 TableDisplayData
    → 动态生成 DataGrid 列 + 绑定行数据
```

---

## 四、开发步骤（按顺序执行）

### Step 1：创建 WPF 项目骨架
- [ ] 新建 `UAssetViewer` WPF 项目（.NET 8，兼容 .NET 9）
- [ ] 添加对 `UAssetAPI` 的 ProjectReference
- [ ] 搭建主窗口布局（左右分栏）
- [ ] 实现 MVVM 基础设施（RelayCommand、ViewModelBase）

### Step 2：实现目录扫描
- [ ] `AssetScannerService`：递归扫描 .uasset，配对 .uexp
- [ ] 尝试加载并分类（DataTable / StringTable / Unknown）
- [ ] 异步扫描 + 进度回调，避免 UI 卡死
- [ ] 左侧面板绑定扫描结果，分组显示

### Step 3：实现 DataTable 查看
- [ ] `DataTableReader`：解析 DataTableExport → 扁平化表格数据
- [ ] `PropertyFormatter`：各类型属性转字符串（从 Phase 0 的 `FormatPropertyValue` 迁移）
- [ ] DataGrid 动态列生成（根据字段名自动创建列）
- [ ] 处理嵌套 Struct 的显示（展开为 `{field=value, ...}` 格式）

### Step 4：实现 StringTable 查看
- [ ] `StringTableReader`：遍历 `FStringTable` (TMap) → Key/Value 列表
- [ ] 固定两列 DataGrid 展示
- [ ] 中文正确显示

### Step 5：错误处理与边界情况
- [ ] 缺少 .uexp 文件时的友好提示
- [ ] 解析异常的 try-catch + 用户提示
- [ ] 空表 / 零行的处理
- [ ] 大文件异步加载（避免 UI 假死）

### Step 6：体验打磨
- [ ] 搜索/过滤功能 (F-008)
- [ ] 列排序 (F-009)
- [ ] 记住上次目录 (F-010)
- [ ] 复制单元格 (F-012)
- [ ] 窗口标题显示当前文件名
- [ ] 状态栏显示行数/条目数

### Step 7：打包发布
- [ ] 配置单文件发布（`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`）
- [ ] 测试独立运行（无需安装 .NET Runtime）
- [ ] 编写简单使用说明

---

## 五、UI 草图

```
┌──────────────────────────────────────────────────────────┐
│  UAsset Viewer                              [—][□][×]   │
├──────────────┬───────────────────────────────────────────┤
│ 📁 打开目录  │  DataTable: MarvelClashTable              │
│              │  行数: 31 | 列数: 57                      │
│ ▼ DataTable  │ ┌──────────────────────────────────────┐  │
│   ├ Marvel.. │ │RowName│ClashLeagueId│ClashGroup│...  │  │
│   ├ Marvel.. │ │───────│─────────────│──────────│──── │  │
│   └ Marvel.. │ │  1    │ PSCup-Apr   │ PSCup    │ ... │  │
│              │ │  2    │ PSCup-May   │ PSCup    │ ... │  │
│ ▼ StringTable│ │  3    │ PSCup-Jun   │ PSCup    │ ... │  │
│   ├ 101_Re.. │ │  ...  │  ...        │  ...     │ ... │  │
│   ├ 103_Ac.. │ └──────────────────────────────────────┘  │
│   └ 117_Us.. │                                           │
│              │ 🔍 搜索: [_______________] [过滤]         │
├──────────────┴───────────────────────────────────────────┤
│ ✅ 已加载 6 个资产 | DataTable: 3 | StringTable: 3       │
└──────────────────────────────────────────────────────────┘
```

---

## 六、风险与注意事项

| 风险 | 影响 | 应对 |
|------|------|------|
| DataTable 动态列过多（57列） | DataGrid 横向滚动体验差 | 支持冻结前几列（RowName）、列宽自适应 |
| 嵌套 Struct 内容过长 | 单元格显示不全 | Tooltip 显示完整内容、双击弹窗查看 |
| 大文件扫描慢 | UI 假死 | 异步扫描 + 进度条 |
| 团队推广时环境差异 | 他人机器没有 .NET | 发布为 self-contained 单文件 |
| UAssetAPI 不支持某些属性类型 | 个别字段显示为 "Unknown" | Phase 0 已验证 1736/1736 字段全部成功，风险极低 |

---

## 七、里程碑

| 阶段 | 内容 | 预计产出 |
|------|------|---------|
| ✅ Phase 0 | 可行性验证 | 验证报告（15/15 通过） |
| 📋 Phase 1 - Step 1~4 | MVP 核心功能 | 能打开目录、查看 DataTable/StringTable |
| 📋 Phase 1 - Step 5~6 | 体验优化 | 搜索、排序、错误处理 |
| 📋 Phase 1 - Step 7 | 打包发布 | 独立可运行的 .exe |
