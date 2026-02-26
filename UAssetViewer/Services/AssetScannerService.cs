using System.IO;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetViewer.Models;

namespace UAssetViewer.Services;

/// <summary>
/// 目录扫描服务：递归扫描 .uasset 文件，自动分类为 DataTable / StringTable / Unknown
/// </summary>
public class AssetScannerService
{
    private readonly EngineVersion _engineVersion;

    public AssetScannerService(EngineVersion engineVersion = EngineVersion.VER_UE5_3)
    {
        _engineVersion = engineVersion;
    }

    /// <summary>
    /// 异步扫描目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="progress">进度回调 (当前数, 总数, 当前文件名)</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<List<ScannedAsset>> ScanAsync(
        string directoryPath,
        IProgress<(int current, int total, string fileName)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScannedAsset>();

        // 递归获取所有 .uasset 文件
        var uassetFiles = Directory.GetFiles(directoryPath, "*.uasset", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToArray();

        for (int i = 0; i < uassetFiles.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = uassetFiles[i];
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            progress?.Report((i + 1, uassetFiles.Length, fileName));

            var asset = await Task.Run(() => ScanSingleFile(filePath), cancellationToken);
            results.Add(asset);
        }

        return results;
    }

    /// <summary>
    /// 扫描单个文件
    /// </summary>
    private ScannedAsset ScanSingleFile(string filePath)
    {
        var scanned = new ScannedAsset
        {
            FilePath = filePath,
            HasExpFile = File.Exists(Path.ChangeExtension(filePath, ".uexp"))
        };

        try
        {
            var uasset = new UAsset(filePath, _engineVersion);

            foreach (var export in uasset.Exports)
            {
                if (export is DataTableExport dtExport)
                {
                    scanned.Type = AssetType.DataTable;
                    scanned.ItemCount = dtExport.Table?.Data?.Count ?? 0;
                    return scanned;
                }

                if (export is StringTableExport stExport)
                {
                    scanned.Type = AssetType.StringTable;
                    scanned.ItemCount = stExport.Table?.Count ?? 0;
                    return scanned;
                }
            }

            // 有 Exports 但不是我们关心的类型
            scanned.Type = AssetType.Unknown;
        }
        catch (Exception ex)
        {
            scanned.Type = AssetType.Unknown;
            scanned.ErrorMessage = ex.Message;
        }

        return scanned;
    }
}