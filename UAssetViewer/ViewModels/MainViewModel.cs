using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using UAssetViewer.Helpers;
using UAssetViewer.Models;
using UAssetViewer.Services;

namespace UAssetViewer.ViewModels;

/// <summary>
/// 主视图模型 — 管理整个应用的状态和交互
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly AssetScannerService _scanner;
    private readonly Services.DataTableReader _dtReader;
    private readonly StringTableReader _stReader;

    // --- 目录与扫描 ---
    private string _currentDirectory = string.Empty;
    public string CurrentDirectory
    {
        get => _currentDirectory;
        set => SetProperty(ref _currentDirectory, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
                OnPropertyChanged(nameof(IsNotScanning));
        }
    }
    public bool IsNotScanning => !IsScanning;

    private string _scanProgress = string.Empty;
    public string ScanProgress
    {
        get => _scanProgress;
        set => SetProperty(ref _scanProgress, value);
    }

    private double _scanProgressPercent;
    public double ScanProgressPercent
    {
        get => _scanProgressPercent;
        set => SetProperty(ref _scanProgressPercent, value);
    }

    // --- 文件列表 ---
    public ObservableCollection<ScannedAsset> DataTableAssets { get; } = new();
    public ObservableCollection<ScannedAsset> StringTableAssets { get; } = new();

    private ScannedAsset? _selectedAsset;
    public ScannedAsset? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
                _ = LoadSelectedAssetAsync();
        }
    }

    // --- 表格数据 ---
    private DataTable? _tableData;
    public DataTable? TableData
    {
        get => _tableData;
        set => SetProperty(ref _tableData, value);
    }

    private string _tableHeader = "请选择一个目录开始";
    public string TableHeader
    {
        get => _tableHeader;
        set => SetProperty(ref _tableHeader, value);
    }

    private string _tableInfo = string.Empty;
    public string TableInfo
    {
        get => _tableInfo;
        set => SetProperty(ref _tableInfo, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // --- 搜索 ---
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilter();
        }
    }

    private DataView? _filteredView;
    public DataView? FilteredView
    {
        get => _filteredView;
        set => SetProperty(ref _filteredView, value);
    }

    // --- 状态栏 ---
    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _totalDataTables;
    public int TotalDataTables
    {
        get => _totalDataTables;
        set => SetProperty(ref _totalDataTables, value);
    }

    private int _totalStringTables;
    public int TotalStringTables
    {
        get => _totalStringTables;
        set => SetProperty(ref _totalStringTables, value);
    }

    // --- Commands ---
    public ICommand OpenDirectoryCommand { get; }
    public ICommand ClearSearchCommand { get; }

    private CancellationTokenSource? _scanCts;

    public MainViewModel()
    {
        _scanner = new AssetScannerService();
        _dtReader = new Services.DataTableReader();
        _stReader = new StringTableReader();

        OpenDirectoryCommand = new RelayCommand(OpenDirectory);
        ClearSearchCommand = new RelayCommand(() =>
        {
            SearchText = string.Empty;
        });
    }

    /// <summary>
    /// 打开目录选择对话框
    /// </summary>
    private void OpenDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择包含 .uasset 文件的目录"
        };

        if (dialog.ShowDialog() == true)
        {
            _ = ScanDirectoryAsync(dialog.FolderName);
        }
    }

    /// <summary>
    /// 扫描指定目录
    /// </summary>
    public async Task ScanDirectoryAsync(string path)
    {
        // 取消之前的扫描
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        CurrentDirectory = path;
        IsScanning = true;
        ErrorMessage = string.Empty;
        DataTableAssets.Clear();
        StringTableAssets.Clear();
        TableData = null;
        FilteredView = null;
        SelectedAsset = null;
        TableHeader = "正在扫描...";
        TableInfo = string.Empty;

        try
        {
            var progress = new Progress<(int current, int total, string fileName)>(p =>
            {
                ScanProgress = $"正在扫描: {p.fileName} ({p.current}/{p.total})";
                ScanProgressPercent = (double)p.current / p.total * 100;
            });

            var results = await _scanner.ScanAsync(path, progress, _scanCts.Token);

            foreach (var asset in results)
            {
                switch (asset.Type)
                {
                    case AssetType.DataTable:
                        DataTableAssets.Add(asset);
                        break;
                    case AssetType.StringTable:
                        StringTableAssets.Add(asset);
                        break;
                }
            }

            TotalDataTables = DataTableAssets.Count;
            TotalStringTables = StringTableAssets.Count;

            int unknownCount = results.Count - DataTableAssets.Count - StringTableAssets.Count;
            StatusText = $"已加载 {results.Count} 个资产 | DataTable: {TotalDataTables} | StringTable: {TotalStringTables}" +
                         (unknownCount > 0 ? $" | 其他: {unknownCount}" : "");

            TableHeader = DataTableAssets.Count + StringTableAssets.Count > 0
                ? "请在左侧选择要查看的资产"
                : "未找到 DataTable 或 StringTable 资产";
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已取消";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"扫描失败: {ex.Message}";
            StatusText = "扫描失败";
        }
        finally
        {
            IsScanning = false;
            ScanProgress = string.Empty;
            ScanProgressPercent = 0;
        }
    }

    /// <summary>
    /// 加载选中的资产
    /// </summary>
    private async Task LoadSelectedAssetAsync()
    {
        if (SelectedAsset == null) return;

        IsLoading = true;
        ErrorMessage = string.Empty;
        SearchText = string.Empty;

        try
        {
            switch (SelectedAsset.Type)
            {
                case AssetType.DataTable:
                    await LoadDataTableAsync(SelectedAsset);
                    break;
                case AssetType.StringTable:
                    await LoadStringTableAsync(SelectedAsset);
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败: {ex.Message}";
            TableHeader = $"加载失败: {SelectedAsset.FileName}";
            TableData = null;
            FilteredView = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDataTableAsync(ScannedAsset asset)
    {
        var dt = await _dtReader.ReadAsync(asset.FilePath);
        TableData = dt;
        FilteredView = dt.DefaultView;
        TableHeader = $"DataTable: {asset.FileName}";
        TableInfo = $"行数: {dt.Rows.Count} | 列数: {dt.Columns.Count}";
        StatusText = $"已加载 DataTable: {asset.FileName} ({dt.Rows.Count} 行, {dt.Columns.Count} 列)";
    }

    private async Task LoadStringTableAsync(ScannedAsset asset)
    {
        var (dt, ns) = await _stReader.ReadAsync(asset.FilePath);
        TableData = dt;
        FilteredView = dt.DefaultView;
        TableHeader = $"StringTable: {asset.FileName}";
        string nsInfo = !string.IsNullOrEmpty(ns) ? $" | Namespace: {ns}" : "";
        TableInfo = $"条目数: {dt.Rows.Count}{nsInfo}";
        StatusText = $"已加载 StringTable: {asset.FileName} ({dt.Rows.Count} 条)";
    }

    /// <summary>
    /// 应用搜索过滤
    /// </summary>
    private void ApplyFilter()
    {
        if (TableData == null)
        {
            FilteredView = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredView = TableData.DefaultView;
            return;
        }

        // 构建 DataView 的 RowFilter
        // 对所有 string 列进行 LIKE 搜索
        var searchTerm = SearchText.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
        var conditions = new List<string>();

        foreach (DataColumn col in TableData.Columns)
        {
            if (col.DataType == typeof(string))
            {
                conditions.Add($"[{col.ColumnName}] LIKE '%{searchTerm}%'");
            }
        }

        try
        {
            var view = new DataView(TableData);
            if (conditions.Count > 0)
            {
                view.RowFilter = string.Join(" OR ", conditions);
            }
            FilteredView = view;

            // 更新状态
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                TableInfo = SelectedAsset?.Type == AssetType.DataTable
                    ? $"行数: {view.Count}/{TableData.Rows.Count} | 列数: {TableData.Columns.Count} | 搜索: \"{SearchText}\""
                    : $"条目数: {view.Count}/{TableData.Rows.Count} | 搜索: \"{SearchText}\"";
            }
        }
        catch
        {
            // RowFilter 语法错误时回退到全量显示
            FilteredView = TableData.DefaultView;
        }
    }
}