using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Hakamiq.Cso.App.Localization;
using Hakamiq.Cso.App.Models;
using Hakamiq.Cso.App.Services;
using Hakamiq.Cso.Core.Formats.Cso;

namespace Hakamiq.Cso.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private UiText text = ArabicUiText.Arabic;
    private string inputPath = string.Empty;
    private string outputPath = string.Empty;
    private bool outputPathWasEditedByUser;
    private bool isSettingOutputPathInternally;
    private UiOperationKind selectedOperation = UiOperationKind.Compress;
    private string selectedProfileName = "game-safe";
    private string blockSizeText = "2048";
    private string workerCountText = "1";
    private bool forceOverwrite;
    private bool deepVerify;
    private bool computeSha256;
    private bool collectCodecReport;
    private bool isBusy;
    private bool isAdvancedOptionsOpen;
    private bool canOpenOutput;
    private string openTargetPath = string.Empty;
    private bool openTargetIsReport = true;
    private string statusText = ArabicUiText.Arabic.Ready;
    private string currentStageText = ArabicUiText.Arabic.Ready;
    private double progressValue;
    private bool isProgressIndeterminate;
    private UiTaskItem? selectedTask;
    private UiOperationKind? lastCompletedOperationKind;
    private bool hasCompletedOperation;
    private bool lastCompletedOperationSucceeded;
    private OperationSummaryViewModel summary = OperationSummaryViewModel.CreateEmpty(ArabicUiText.Arabic);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UiLogEntry> LogEntries { get; } = [];

    public ObservableCollection<UiTaskItem> Tasks { get; } = [];

    public UiText Text
    {
        get => text;
        private set
        {
            if (SetField(ref text, value))
            {
                OnPropertyChanged(nameof(LanguageToggleText));
                OnPropertyChanged(nameof(SelectedOperationName));
                OnPropertyChanged(nameof(SelectedOperationDescription));
                OnPropertyChanged(nameof(OutputRequirementText));
                OnPropertyChanged(nameof(CompactOutputText));
                OnPropertyChanged(nameof(OpenResultText));
                RefreshTaskStatuses();
                RefreshLocalizedStatusText();
            }
        }
    }

    public string LanguageToggleText => Text.Language == UiLanguage.Arabic ? "English" : "العربية";

    public string InputPath
    {
        get => inputPath;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (SetField(ref inputPath, normalized))
            {
                RefreshSuggestedOutputPath();
            }
        }
    }

    public string OutputPath
    {
        get => outputPath;
        set
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (SetField(ref outputPath, normalized) && !isSettingOutputPathInternally)
            {
                outputPathWasEditedByUser = !string.IsNullOrWhiteSpace(normalized);
            }

            ClearOpenTarget();
            OnPropertyChanged(nameof(CompactOutputText));
        }
    }

    public UiOperationKind SelectedOperation
    {
        get => selectedOperation;
        set
        {
            if (SetField(ref selectedOperation, value))
            {
                ApplyOperationDefaults();
                OnOperationPresentationChanged();
                RefreshSuggestedOutputPath();
            }
        }
    }

    public string SelectedProfileName
    {
        get => selectedProfileName;
        set => SetField(ref selectedProfileName, value?.Trim() ?? string.Empty);
    }

    public string BlockSizeText
    {
        get => blockSizeText;
        set => SetField(ref blockSizeText, value?.Trim() ?? string.Empty);
    }

    public string WorkerCountText
    {
        get => workerCountText;
        set => SetField(ref workerCountText, value?.Trim() ?? string.Empty);
    }

    public bool ForceOverwrite
    {
        get => forceOverwrite;
        set => SetField(ref forceOverwrite, value);
    }

    public bool DeepVerify
    {
        get => deepVerify;
        set => SetField(ref deepVerify, value);
    }

    public bool ComputeSha256
    {
        get => computeSha256;
        set => SetField(ref computeSha256, value);
    }

    public bool CollectCodecReport
    {
        get => collectCodecReport;
        set => SetField(ref collectCodecReport, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanEdit));
                OnOptionAvailabilityChanged();
            }
        }
    }

    public bool CanEdit => !IsBusy;

    public bool RequiresOutputPath => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Decompress or UiOperationKind.Repair;

    public bool CanBrowseOutput => CanEdit && RequiresOutputPath;

    public bool CanUseProfile => CanEdit && SelectedOperationUsesProfile;

    public bool CanUseForceOverwrite => CanEdit && RequiresOutputPath;

    public bool CanUseWorkerCount => CanEdit && SelectedOperationUsesWorkerCount;

    public bool CanUseBlockSize => CanEdit && SelectedOperationUsesBlockSize;

    public bool CanUseDeepVerify => CanEdit && SelectedOperationCanToggleDeepVerify;

    public bool CanUseSha256 => CanEdit && SelectedOperation is UiOperationKind.Verify;

    public bool CanUseCodecReport => CanEdit && SelectedOperationUsesCodecReport;

    public bool CanOpenOutput => !IsBusy && canOpenOutput;

    public string OpenTargetPath => openTargetPath;

    public string OpenResultText => openTargetIsReport
        ? Text.Language == UiLanguage.Arabic ? "فتح التقرير" : "Open report"
        : Text.OpenOutput;

    public bool IsAdvancedOptionsOpen
    {
        get => isAdvancedOptionsOpen;
        set => SetField(ref isAdvancedOptionsOpen, value);
    }

    public UiTaskItem? SelectedTask
    {
        get => selectedTask;
        set
        {
            if (SetField(ref selectedTask, value) && value is not null)
            {
                SetInputPathFromTask(value);
            }
        }
    }

    public string CompactOutputText => RequiresOutputPath && !string.IsNullOrWhiteSpace(OutputPath)
        ? Path.GetFileName(OutputPath)
        : Text.AutomaticOutput;

    public string SelectedOperationName => GetOperationName(SelectedOperation);

    public string SelectedOperationDescription => GetOperationDescription(SelectedOperation);

    public string OutputRequirementText => RequiresOutputPath
        ? Text.OutputFileHint
        : Text.OutputPathNotRequired;

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string CurrentStageText
    {
        get => currentStageText;
        private set => SetField(ref currentStageText, value);
    }

    public double ProgressValue
    {
        get => progressValue;
        private set => SetField(ref progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => isProgressIndeterminate;
        private set => SetField(ref isProgressIndeterminate, value);
    }

    public OperationSummaryViewModel Summary
    {
        get => summary;
        private set => SetField(ref summary, value);
    }

    public void ToggleLanguage()
    {
        Text = Text.Language == UiLanguage.Arabic
            ? ArabicUiText.English
            : ArabicUiText.Arabic;

        if (!IsBusy && LogEntries.Count == 0)
        {
            Summary = OperationSummaryViewModel.CreateEmpty(Text);
        }
    }

    public void ClearLog()
    {
        LogEntries.Clear();
        Tasks.Clear();
        selectedTask = null;
        hasCompletedOperation = false;
        lastCompletedOperationKind = null;
        lastCompletedOperationSucceeded = false;
        OnPropertyChanged(nameof(SelectedTask));
        ClearOpenTarget();
        StatusText = Text.Ready;
        CurrentStageText = Text.Ready;
        ProgressValue = 0;
        IsProgressIndeterminate = false;
        Summary = OperationSummaryViewModel.CreateEmpty(Text);
    }

    public void SetInputPathFromUser(string path)
    {
        SetInputPathsFromUser(new[] { path });
    }

    public void SetInputPathsFromUser(IEnumerable<string> paths)
    {
        string[] normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            return;
        }

        outputPathWasEditedByUser = false;
        ClearOpenTarget();

        UiTaskItem? taskToSelect = null;

        foreach (string path in normalizedPaths)
        {
            UiTaskItem? existingTask = Tasks.FirstOrDefault(task =>
                string.Equals(task.Path, path, StringComparison.OrdinalIgnoreCase));

            if (existingTask is not null)
            {
                existingTask.SetStatus(CreateTaskStatusForCurrentOperation(existingTask.Path));
                taskToSelect ??= existingTask;
                continue;
            }

            UiTaskItem task = UiTaskItem.Create(path, CreateTaskStatusForCurrentOperation(path));
            Tasks.Add(task);
            taskToSelect ??= task;
            AddLog(Text.Input, path, "Info");
        }

        if (taskToSelect is not null)
        {
            SelectedTask = taskToSelect;
        }
    }

    public void SetOutputPathFromUser(string path)
    {
        OutputPath = path;
        ClearOpenTarget();
        outputPathWasEditedByUser = !string.IsNullOrWhiteSpace(path);

        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            AddLog(Text.Output, OutputPath, "Info");
        }
    }

    public void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsOpen = !IsAdvancedOptionsOpen;
    }

    public string GetSuggestedOutputPath()
    {
        return CreateSuggestedOutputPath(InputPath, SelectedOperation);
    }

    public async Task RunSelectedOperationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            OperationRequest request = CreateRequest();
            UiTaskItem? activeTask = SelectedTask;
            string operationName = GetOperationName(request.Kind);

            IsBusy = true;
            ClearOpenTarget();
            StatusText = $"{Text.Running}: {operationName}";
            CurrentStageText = CreatePreparingStageText(request.Kind);
            activeTask?.SetStatus(CurrentStageText);
            ProgressValue = 0;
            IsProgressIndeterminate = true;

            AddLog(Text.OperationStarted, BuildOperationStartMessage(request, operationName), "Info");

            Progress<double> progress = new(value =>
            {
                IsProgressIndeterminate = false;
                ProgressValue = Math.Clamp(value, 0, 100);
                CurrentStageText = CreateProgressStageText(request.Kind, ProgressValue);
                activeTask?.SetStatus(CurrentStageText);
            });

            CsoUiOperationResult result = await Task.Run(() => ExecuteRequest(request, progress)).ConfigureAwait(true);

            IsProgressIndeterminate = false;
            ProgressValue = result.Success ? 100 : 0;
            hasCompletedOperation = true;
            lastCompletedOperationKind = request.Kind;
            lastCompletedOperationSucceeded = result.Success;
            StatusText = result.Success ? Text.OperationSucceeded : Text.OperationFailed;
            CurrentStageText = result.Success ? CreateCompletedStageText(request.Kind) : Text.OperationFailed;
            activeTask?.SetStatus(CurrentStageText);
            string? reportPath = TryWriteOperationReport(request, result, operationName);
            OperationOpenTarget? openTarget = ResolveOpenTarget(request, reportPath);

            if (openTarget is { } target)
            {
                SetOpenTarget(target.Path, target.IsReport);
            }
            else
            {
                ClearOpenTarget();
            }

            Summary = CreateSummary(result, operationName);

            AddLog(
                result.Success ? Text.OperationSucceeded : Text.OperationFailed,
                operationName,
                result.Success ? "Success" : "Error");

            if (!string.IsNullOrWhiteSpace(result.Details))
            {
                AddLog(Text.TechnicalDetails, SimplifyDetailsForUser(result.Details), "Info");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            hasCompletedOperation = true;
            lastCompletedOperationKind = SelectedOperation;
            lastCompletedOperationSucceeded = false;
            StatusText = Text.OperationFailed;
            CurrentStageText = Text.OperationFailed;
            SelectedTask?.SetStatus(CurrentStageText);
            Summary = new OperationSummaryViewModel(
                Text.OperationFailed,
                ex.Message,
                "-",
                "-",
                "-");

            AddLog(Text.Error, ex.Message, "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private OperationRequest CreateRequest()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            throw new InvalidOperationException(Text.InputPathRequired);
        }

        CsoCompressionProfile profile = SelectedOperationUsesProfile
            ? GetSelectedProfile()
            : CsoCompressionProfile.GameSafe;
        uint blockSize = SelectedOperationUsesBlockSize
            ? GetBlockSize()
            : CsoCompressor.DefaultBlockSize;
        int workerCount = SelectedOperationUsesWorkerCount
            ? GetWorkerCount()
            : 1;
        string effectiveOutputPath = RequiresOutputPath
            ? GetOrCreateOutputPath()
            : string.Empty;

        return new OperationRequest(
            SelectedOperation,
            InputPath,
            effectiveOutputPath,
            profile,
            blockSize,
            workerCount,
            ForceOverwrite,
            SelectedOperationUsesDeepVerify && DeepVerify,
            SelectedOperation is UiOperationKind.Verify && ComputeSha256,
            SelectedOperationUsesCodecReport && CollectCodecReport);
    }

    private string GetOrCreateOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            return OutputPath;
        }

        string suggested = GetSuggestedOutputPath();

        if (string.IsNullOrWhiteSpace(suggested))
        {
            throw new InvalidOperationException(Text.OperationDoesNotUseOutput);
        }

        SetOutputPathInternally(suggested);
        return suggested;
    }

    private CsoCompressionProfile GetSelectedProfile()
    {
        if (!CsoCompressionProfilePolicy.TryParse(SelectedProfileName, out CsoCompressionProfile profile))
        {
            throw new InvalidOperationException(Text.InvalidCompressionProfile);
        }

        return profile;
    }

    private uint GetBlockSize()
    {
        if (!uint.TryParse(BlockSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint blockSize) || blockSize == 0)
        {
            throw new InvalidOperationException(Text.InvalidBlockSize);
        }

        return blockSize;
    }

    private int GetWorkerCount()
    {
        if (!int.TryParse(WorkerCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int workerCount) || workerCount < 1)
        {
            throw new InvalidOperationException(Text.InvalidThreads);
        }

        return workerCount;
    }

    private void RefreshSuggestedOutputPath()
    {
        if (outputPathWasEditedByUser || string.IsNullOrWhiteSpace(InputPath))
        {
            return;
        }

        string suggested = CreateSuggestedOutputPath(InputPath, SelectedOperation);
        SetOutputPathInternally(suggested);
        OnPropertyChanged(nameof(CompactOutputText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private void SetOutputPathInternally(string value)
    {
        isSettingOutputPathInternally = true;

        try
        {
            OutputPath = value;
        }
        finally
        {
            isSettingOutputPathInternally = false;
        }
    }

    private static string CreateSuggestedOutputPath(string inputPath, UiOperationKind operationKind)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return string.Empty;
        }

        try
        {
            return operationKind switch
            {
                UiOperationKind.Compress => CsoUiOperationService.CreateSuggestedCompressOutputPath(inputPath),
                UiOperationKind.Decompress => CsoUiOperationService.CreateSuggestedDecompressOutputPath(inputPath),
                UiOperationKind.Repair => CsoUiOperationService.CreateSuggestedRepairOutputPath(inputPath),
                _ => string.Empty,
            };
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (NotSupportedException)
        {
            return string.Empty;
        }
        catch (PathTooLongException)
        {
            return string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static CsoUiOperationResult ExecuteRequest(OperationRequest request, IProgress<double> progress)
    {
        return request.Kind switch
        {
            UiOperationKind.Detect => CsoUiOperationService.Detect(request.InputPath),
            UiOperationKind.Analyze => CsoUiOperationService.Analyze(request.InputPath),
            UiOperationKind.Measure => CsoUiOperationService.Measure(
                request.InputPath,
                request.Profile,
                request.BlockSize,
                progress),
            UiOperationKind.Compress => CsoUiOperationService.Compress(
                request.InputPath,
                request.OutputPath,
                request.Profile,
                request.BlockSize,
                request.WorkerCount,
                request.ForceOverwrite,
                request.DeepVerify,
                request.CodecReport,
                progress),
            UiOperationKind.Decompress => CsoUiOperationService.Decompress(
                request.InputPath,
                request.OutputPath,
                request.ForceOverwrite,
                progress),
            UiOperationKind.Verify => CsoUiOperationService.Verify(
                request.InputPath,
                request.DeepVerify,
                request.ComputeSha256,
                progress),
            UiOperationKind.Repair => CsoUiOperationService.Repair(
                request.InputPath,
                request.OutputPath,
                request.Profile,
                request.ForceOverwrite,
                request.DeepVerify,
                request.CodecReport,
                progress),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, ArabicUiText.English.UnsupportedOperation),
        };
    }

    private OperationSummaryViewModel CreateSummary(CsoUiOperationResult result, string operationName)
    {
        string details = result.Details ?? string.Empty;
        string originalSize = ExtractValue(details, "Original bytes:") ?? ExtractValue(details, "Bytes read:") ?? "-";
        string resultSize = ExtractValue(details, "Estimated bytes:") ?? ExtractValue(details, "Bytes written:") ?? "-";
        string savedSize = TryCreateSavingsText(originalSize, resultSize);

        return new OperationSummaryViewModel(
            result.Success ? Text.OperationSucceeded : Text.OperationFailed,
            operationName,
            originalSize,
            resultSize,
            savedSize);
    }

    private static string TryCreateSavingsText(string originalSizeText, string resultSizeText)
    {
        long? originalSize = ParseFormattedLong(originalSizeText);
        long? resultSize = ParseFormattedLong(resultSizeText);

        if (originalSize is null || resultSize is null || originalSize <= 0 || resultSize < 0)
        {
            return "-";
        }

        long saved = originalSize.Value - resultSize.Value;
        double savedPercent = saved / (double)originalSize.Value;

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0:N0} ({1:P2})",
            saved,
            savedPercent);
    }

    private static long? ParseFormattedLong(string text)
    {
        char[] digits = [.. text.Where(char.IsDigit)];

        if (long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
        {
            return value;
        }

        return null;
    }

    private static string? ExtractValue(string details, string label)
    {
        using StringReader reader = new(details);

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                return line[label.Length..].Trim();
            }
        }

        return null;
    }

    private string? TryWriteOperationReport(
        OperationRequest request,
        CsoUiOperationResult result,
        string operationName)
    {
        try
        {
            string directory = GetReportDirectory(request);
            Directory.CreateDirectory(directory);

            string reportPath = CreateUniqueReportPath(directory, request);
            string report = BuildOperationReport(request, result, operationName);
            File.WriteAllText(reportPath, report, Encoding.UTF8);
            AddLog(Text.Language == UiLanguage.Arabic ? "التقرير" : "Report", reportPath, "Info");
            return reportPath;
        }
        catch (ArgumentException ex)
        {
            AddLog(Text.Error, ex.Message, "Error");
            return null;
        }
        catch (IOException ex)
        {
            AddLog(Text.Error, ex.Message, "Error");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            AddLog(Text.Error, ex.Message, "Error");
            return null;
        }
        catch (NotSupportedException ex)
        {
            AddLog(Text.Error, ex.Message, "Error");
            return null;
        }
    }

    private static string GetReportDirectory(OperationRequest request)
    {
        string anchorPath = !string.IsNullOrWhiteSpace(request.OutputPath)
            ? request.OutputPath
            : request.InputPath;

        string fullPath = Path.GetFullPath(anchorPath);
        string? directory = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        return string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : directory;
    }

    private static string CreateUniqueReportPath(string directory, OperationRequest request)
    {
        string baseName = CreateReportBaseName(request);
        string reportKind = CreateReportKindName(request.Kind);
        string candidate = Path.Combine(directory, $"{baseName}.{reportKind}.txt");

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        for (int number = 2; number < int.MaxValue; number++)
        {
            candidate = Path.Combine(directory, $"{baseName}.{reportKind}-{number}.txt");

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create a unique operation report path.");
    }

    private static string CreateReportBaseName(OperationRequest request)
    {
        string anchorPath = !string.IsNullOrWhiteSpace(request.InputPath)
            ? request.InputPath
            : request.OutputPath;
        string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(anchorPath));

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return "csokit";
        }

        return StripGeneratedSuffixes(baseName);
    }

    private static string CreateReportKindName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => "compress-report",
            UiOperationKind.Decompress => "decompress-report",
            UiOperationKind.Verify => "verify-report",
            UiOperationKind.Repair => "repair-report",
            UiOperationKind.Detect => "detect-report",
            UiOperationKind.Analyze => "analyze-report",
            UiOperationKind.Measure => "measure-report",
            _ => "operation-report",
        };
    }

    private static string StripGeneratedSuffixes(string baseName)
    {
        string normalized = baseName.Trim();

        while (normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(" - Hakamiq Repaired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^".repaired".Length].Trim()
                : normalized[..^" - Hakamiq Repaired".Length].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized) ? "csokit" : normalized;
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);

        foreach (char c in value)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        return builder.ToString().Trim();
    }

    private string BuildOperationReport(
        OperationRequest request,
        CsoUiOperationResult result,
        string operationName)
    {
        return Text.Language == UiLanguage.Arabic
            ? BuildArabicOperationReport(request, result, operationName)
            : BuildEnglishOperationReport(request, result, operationName);
    }

    private static string BuildEnglishOperationReport(
        OperationRequest request,
        CsoUiOperationResult result,
        string operationName)
    {
        StringBuilder builder = new();
        builder.AppendLine("Hakamiq CsoKit Operation Report");
        builder.AppendLine($"Generated: {DateTime.Now:O}");
        builder.AppendLine($"Operation: {FormatEnglishOperationName(operationName, request.Kind)}");
        builder.AppendLine($"Success: {result.Success}");
        builder.AppendLine($"Status: {result.Status}");
        builder.AppendLine($"Input: {request.InputPath}");

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            builder.AppendLine($"Output: {request.OutputPath}");
        }

        builder.AppendLine();
        builder.AppendLine("Details:");
        builder.AppendLine(result.Details ?? string.Empty);
        return builder.ToString();
    }

    private string BuildArabicOperationReport(
        OperationRequest request,
        CsoUiOperationResult result,
        string operationName)
    {
        StringBuilder builder = new();
        builder.AppendLine("تقرير عمليات Hakamiq CsoKit");
        builder.AppendLine($"تم الإنشاء: {DateTime.Now:O}");
        builder.AppendLine($"العملية: {FormatArabicOperationName(operationName, request.Kind)}");
        builder.AppendLine($"النتيجة النهائية: {FormatArabicSuccess(result.Success)}");
        builder.AppendLine($"الحالة: {TranslateStatusToArabic(result.Status)}");
        builder.AppendLine($"الإدخال: {request.InputPath}");

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            builder.AppendLine($"الإخراج: {request.OutputPath}");
        }

        builder.AppendLine();
        builder.AppendLine("التفاصيل:");
        builder.AppendLine(TranslateDetailsToArabic(result.Details ?? string.Empty));
        return builder.ToString();
    }

    private static string CreateArabicOperationKindName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => "ضغط",
            UiOperationKind.Decompress => "فك الضغط",
            UiOperationKind.Verify => "فحص السلامة",
            UiOperationKind.Repair => "إصلاح الملف",
            UiOperationKind.Detect => "كشف الصيغة",
            UiOperationKind.Analyze => "تحليل ISO",
            UiOperationKind.Measure => "تقدير الحجم",
            _ => operationKind.ToString(),
        };
    }

    private static string FormatEnglishOperationName(string operationName, UiOperationKind operationKind)
    {
        string kindName = operationKind.ToString();
        return string.Equals(operationName, kindName, StringComparison.OrdinalIgnoreCase)
            ? operationName
            : $"{operationName} ({kindName})";
    }

    private static string FormatArabicOperationName(string operationName, UiOperationKind operationKind)
    {
        string kindName = CreateArabicOperationKindName(operationKind);
        return string.Equals(operationName, kindName, StringComparison.Ordinal)
            ? operationName
            : $"{operationName} ({kindName})";
    }

    private static string FormatArabicBoolean(bool value)
    {
        return value ? "نعم" : "لا";
    }

    private static string FormatArabicSuccess(bool value)
    {
        return value ? "نجاح" : "فشل";
    }

    private static string TranslateStatusToArabic(string status)
    {
        return status switch
        {
            "Deep verify passed; no corruption detected" => "اجتاز الفحص العميق؛ لم يُكتشف أي تلف",
            "Deep verify failed; corruption detected" => "فشل الفحص العميق؛ تم اكتشاف تلف أو مشكلة بنيوية",
            "Deep verify failed; no corruption verdict" => "فشل الفحص العميق؛ لم يصدر حكم تلف",
            "Shallow verify passed; no header/index corruption detected" => "اجتاز الفحص السطحي؛ لم تُكتشف مشكلة في الرأس أو الفهرس",
            "Shallow verify failed; structural issues detected" => "فشل الفحص السطحي؛ تم اكتشاف مشاكل بنيوية",
            "Verify failed; input format was not recognized" => "فشل الفحص؛ لم يتم التعرف على صيغة الإدخال",
            "Verify failed; unsupported shallow format" => "فشل الفحص؛ الصيغة غير مدعومة في الفحص السطحي",
            "Rebuild completed; no input corruption was proven" => "اكتملت إعادة البناء؛ لم يثبت وجود تلف في الإدخال",
            "Repair completed; recoverable input issues were detected" => "اكتمل الإصلاح؛ تم اكتشاف مشاكل قابلة للاسترداد في الإدخال",
            "Repair failed; re-dump required" => "فشل الإصلاح؛ يلزم إعادة نسخ الملف من المصدر",
            "Repair failed after detecting input issues" => "فشل الإصلاح بعد اكتشاف مشاكل في الإدخال",
            "Repair failed" => "فشل الإصلاح",
            "Compress completed" => "اكتمل الضغط",
            "Compress failed" => "فشل الضغط",
            "Decompress completed" => "اكتمل فك الضغط",
            "Decompress failed" => "فشل فك الضغط",
            "Detect completed" => "اكتمل كشف الصيغة",
            "Detect failed" => "فشل كشف الصيغة",
            "Analyze completed" => "اكتمل التحليل",
            "Analyze failed" => "فشل التحليل",
            "Measure completed" => "اكتمل تقدير الحجم",
            "Measure failed" => "فشل تقدير الحجم",
            _ => status,
        };
    }

    private static string TranslateDetailsToArabic(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return string.Empty;
        }

        StringBuilder builder = new(details.Length);
        using StringReader reader = new(details);

        while (reader.ReadLine() is { } line)
        {
            builder.AppendLine(TranslateDetailLineToArabic(line));
        }

        return builder.ToString().TrimEnd();
    }

    private static string TranslateDetailLineToArabic(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        string trimmed = line.Trim();

        if (ArabicSectionNames.TryGetValue(trimmed, out string? sectionName))
        {
            return sectionName;
        }

        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            return "- " + TranslateIssueTextToArabic(trimmed[2..]);
        }

        int separatorIndex = trimmed.IndexOf(':');
        if (separatorIndex < 0)
        {
            return TranslateKnownValueToArabic(trimmed);
        }

        string label = trimmed[..separatorIndex];
        string value = trimmed[(separatorIndex + 1)..].Trim();
        string translatedLabel = ArabicDetailLabels.TryGetValue(label, out string? mappedLabel)
            ? mappedLabel
            : label;

        return $"{translatedLabel}: {TranslateDetailValueToArabic(label, value)}";
    }

    private static string TranslateDetailValueToArabic(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (string.Equals(label, "Input", StringComparison.Ordinal) ||
            string.Equals(label, "Output", StringComparison.Ordinal) ||
            string.Equals(label, "Format", StringComparison.Ordinal) ||
            string.Equals(label, "Magic", StringComparison.Ordinal) ||
            string.Equals(label, "Title", StringComparison.Ordinal) ||
            string.Equals(label, "DISC_ID UMD_DATA", StringComparison.Ordinal) ||
            string.Equals(label, "DISC_ID PARAM.SFO", StringComparison.Ordinal))
        {
            return value;
        }

        if (string.Equals(label, "SHA256", StringComparison.Ordinal) ||
            string.Equals(label, "Reconstructed SHA256", StringComparison.Ordinal))
        {
            return string.Equals(value, "Disabled", StringComparison.Ordinal) ? "معطّل" : value;
        }

        if (string.Equals(label, "Output written", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — هذه عملية فحص فقط ولم يتم إنشاء ملف إخراج", trueText: "نعم — تم إنشاء ملف إخراج");
        }

        if (string.Equals(label, "Corruption detected", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — لم يثبت وجود تلف", trueText: "نعم — تم اكتشاف تلف أو خلل بنيوي");
        }

        if (string.Equals(label, "Repair needed", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — لا يحتاج إصلاح", trueText: "نعم — يحتاج إصلاح أو إعادة نسخ حسب نوع المشكلة");
        }

        if (string.Equals(label, "Issues", StringComparison.Ordinal))
        {
            return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? "لا توجد" : TranslateKnownValueToArabic(value);
        }

        return TranslateKnownValueToArabic(TranslateValueFragmentsToArabic(value));
    }

    private static string TranslateValueFragmentsToArabic(string value)
    {
        return value
            .Replace(" of indexed blocks", " من الأجزاء المفهرسة", StringComparison.Ordinal)
            .Replace(" MiB/s", " ميبي بايت/ثانية", StringComparison.Ordinal)
            .Replace(" MiB", " ميبي بايت", StringComparison.Ordinal)
            .Replace(" bytes", " بايت", StringComparison.Ordinal);
    }

    private static string TranslateBooleanVerdict(string value, string falseText, string trueText)
    {
        return value switch
        {
            "False" => falseText,
            "True" => trueText,
            "No" => falseText,
            "Yes" => trueText,
            "Unknown" => "غير معروف — لا يمكن إصدار حكم من هذه العملية",
            _ => TranslateKnownValueToArabic(TranslateValueFragmentsToArabic(value)),
        };
    }

    private static string TranslateKnownValueToArabic(string value)
    {
        return value switch
        {
            "True" => "نعم",
            "False" => "لا",
            "none" => "لا توجد",
            "None" => "لا توجد",
            "Unknown" => "غير معروف",
            "Yes" => "نعم",
            "No" => "لا",
            "Passed" => "سليم",
            "Failed" => "فشل",
            "Deep" => "عميق",
            "Shallow" => "سطحي",
            "Disabled" => "معطّل",
            "N/A" => "غير منطبق",
            "Not completed" => "لم يكتمل",
            "Not determined by shallow verify" => "غير محدد بواسطة الفحص السطحي",
            "N/A for this container reader" => "لا ينطبق على قارئ هذه الحاوية",
            "Passed via container reader" => "سليم عبر قارئ الحاوية",
            "Hybrid CSO verification" => "فحص CSO هجين",
            "Hybrid container verification" => "فحص حاوية هجين",
            "Header + index + block payload reconstruction" => "رأس الملف + الفهرس + إعادة بناء بيانات الأجزاء",
            "Container header + block payload reconstruction" => "رأس الحاوية + إعادة بناء بيانات الأجزاء",
            "Legacy structural CSO header/index validation" => "تحقق بنيوي محافظ لرأس CSO وفهرسه",
            "Streaming payload decode with pooled compressed buffers" => "فك بيانات متدفق باستخدام مخازن مضغوطة معاد استخدامها",
            "Coverage, topology, bounds, and reconstruction diagnostics" => "التغطية والبنية والحدود وتشخيص إعادة البناء",
            "Coverage, zero-block, and reconstruction diagnostics" => "التغطية والأجزاء الصفرية وتشخيص إعادة البناء",
            "The file was read block-by-block and payload data was reconstructed in memory. No repair output was produced." => "تمت قراءة الملف جزءاً بجزء، وأُعيد بناء بياناته داخل الذاكرة. لم يتم إنشاء ملف إخراج أو ملف إصلاح.",
            "Header and index metadata were inspected only; compressed block payloads were not decompressed." => "تم فحص بيانات الرأس والفهرس فقط؛ لم يتم فك ضغط بيانات الأجزاء.",
            "No corruption was detected by deep verification. The input was readable and all checked payload blocks reconstructed successfully." => "لم يكتشف الفحص العميق أي تلف. كان الإدخال قابلاً للقراءة، وتمت إعادة بناء كل الأجزاء المفحوصة بنجاح.",
            "Corruption or unsupported container structure was detected. The file did not fully reconstruct under deep verification." => "تم اكتشاف تلف أو بنية حاوية غير مدعومة. لم يكتمل إعادة بناء الملف أثناء الفحص العميق.",
            "This verification validates container structure, index/bounds semantics, and payload decompression. It does not prove Redump hash match, game database identity, or gameplay correctness." => "يتحقق هذا الفحص من بنية الحاوية، ومنطق الفهرس والحدود، وفك ضغط البيانات. لا يثبت هذا الفحص تطابق Redump، أو هوية اللعبة في قواعد البيانات، أو صحة التشغيل داخل اللعبة.",
            "No header/index corruption was detected. This is a metadata-only pass and does not prove that every compressed block can be decompressed." => "لم يتم اكتشاف تلف في الرأس أو الفهرس. هذا فحص بيانات وصفية فقط ولا يثبت أن كل جزء مضغوط قابل لفك الضغط.",
            "Structural CSO metadata issues were detected. Run Deep verify or Repair to classify the damage." => "تم اكتشاف مشاكل بنيوية في بيانات CSO الوصفية. شغّل الفحص العميق أو الإصلاح لتصنيف الضرر.",
            "Yes or re-dump required; see Issues for the exact failing block/condition." => "نعم أو يلزم إعادة النسخ؛ راجع المشاكل لمعرفة الجزء أو الشرط الفاشل بدقة.",
            "Counted after payload decode; may overlap compressed/stored block counts." => "تُحسب بعد فك بيانات الأجزاء، وقد تتداخل مع عدد الأجزاء المضغوطة أو المخزنة.",
            "N/A for raw image" => "غير منطبق على الصورة الخام",
            "Hybrid raw ISO verification" => "فحص ISO خام هجين",
            "ISO9660 probe + raw sector read + full payload reconstruction" => "استكشاف ISO9660 + قراءة قطاعات خام + إعادة بناء كاملة للبيانات",
            "ISO9660 primary-volume probe and strict 2048-byte sector-alignment validation" => "استكشاف واصف ISO9660 الأساسي والتحقق الصارم من محاذاة قطاعات 2048 بايت",
            "Sequential raw-sector read with pooled output buffers" => "قراءة قطاعات خام متتابعة باستخدام مخازن إخراج معاد استخدامها",
            "Coverage, zero-content, bounds, and reconstruction diagnostics" => "التغطية والمحتوى الصفري والحدود وتشخيص إعادة البناء",
            "Not reached because raw ISO alignment validation failed." => "لم يتم الوصول إلى هذه الطبقة لأن التحقق من محاذاة ISO الخام فشل.",
            "No raw-image read, alignment, or reconstruction problems were detected. The input was readable and every checked sector reconstructed successfully." => "لم تُكتشف مشاكل قراءة أو محاذاة أو إعادة بناء في الصورة الخام. كان الإدخال قابلاً للقراءة، وتمت إعادة بناء كل القطاعات المفحوصة بنجاح.",
            "Raw-image read, alignment, or unsupported container structure failed. The file did not fully reconstruct under deep verification." => "فشلت قراءة الصورة الخام أو المحاذاة أو بنية الحاوية غير المدعومة. لم تكتمل إعادة بناء الملف أثناء الفحص العميق.",
            "This verification validates raw image readability, 2048-byte sector alignment, full block coverage, and payload reconstruction. It does not prove Redump hash match, game database identity, or gameplay correctness." => "يتحقق هذا الفحص من قابلية قراءة الصورة الخام، ومحاذاة قطاعات 2048 بايت، وتغطية كل الأجزاء، وإعادة بناء البيانات. لا يثبت هذا الفحص تطابق Redump، أو هوية اللعبة في قواعد البيانات، أو صحة التشغيل داخل اللعبة.",
            _ => value,
        };
    }

    private static string TranslateIssueTextToArabic(string issueText)
    {
        if (string.Equals(issueText, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "لا توجد";
        }

        return issueText;
    }

    private static readonly Dictionary<string, string> ArabicSectionNames = new(StringComparer.Ordinal)
    {
        ["Verification layers:"] = "طبقات الفحص:",
        ["Integrity checks:"] = "فحوصات السلامة:",
        ["CSO metadata:"] = "بيانات CSO الوصفية:",
        ["Raw image metadata:"] = "بيانات الصورة الخام:",
        ["Forensic statistics:"] = "إحصائيات الفحص:",
        ["Warnings:"] = "تحذيرات:",
        ["Issues:"] = "المشاكل:",
        ["Codec wins:"] = "نتائج codecs:",
    };

    private static readonly Dictionary<string, string> ArabicDetailLabels = new(StringComparer.Ordinal)
    {
        ["Input"] = "الإدخال",
        ["Output"] = "الإخراج",
        ["Verification type"] = "نوع الفحص",
        ["Output written"] = "هل تم إنشاء ملف إخراج",
        ["Action taken"] = "الإجراء المتخذ",
        ["Format"] = "الصيغة",
        ["CSO version"] = "إصدار CSO",
        ["Block size"] = "حجم الجزء",
        ["Index shift"] = "إزاحة الفهرس",
        ["Uncompressed size"] = "الحجم بعد فك الضغط",
        ["Compressed file size"] = "حجم الملف المضغوط",
        ["Container ratio"] = "نسبة الحاوية",
        ["Space saved"] = "المساحة الموفرة",
        ["Algorithm"] = "الخوارزمية",
        ["Scope"] = "النطاق",
        ["Legacy layer"] = "الطبقة القديمة",
        ["Modern layer"] = "الطبقة الحديثة",
        ["Forensic layer"] = "طبقة التشخيص",
        ["Header check"] = "فحص الرأس",
        ["Index check"] = "فحص الفهرس",
        ["Final sentinel"] = "المؤشر النهائي",
        ["Block offset order"] = "ترتيب إزاحات الأجزاء",
        ["Bounds check"] = "فحص الحدود",
        ["Payload decode"] = "فك البيانات",
        ["Reconstructed size"] = "حجم إعادة البناء",
        ["Result"] = "النتيجة",
        ["Corruption detected"] = "هل تم اكتشاف تلف",
        ["Coverage"] = "التغطية",
        ["Blocks checked"] = "الأجزاء المفحوصة",
        ["Bytes reconstructed"] = "البايتات المعاد بناؤها",
        ["Expected reconstructed bytes"] = "البايتات المتوقعة بعد إعادة البناء",
        ["File length"] = "طول الملف",
        ["Header size"] = "حجم الرأس",
        ["Index entries"] = "مدخلات الفهرس",
        ["Index table bytes"] = "بايتات جدول الفهرس",
        ["Index end offset"] = "إزاحة نهاية الفهرس",
        ["First data offset"] = "أول إزاحة بيانات",
        ["Final data offset"] = "آخر إزاحة بيانات",
        ["Physical payload bytes"] = "بايتات البيانات الفعلية",
        ["Payload blocks decoded"] = "أجزاء البيانات المفكوكة",
        ["Compressed blocks"] = "الأجزاء المضغوطة",
        ["Stored blocks"] = "الأجزاء المخزنة",
        ["Zero-content blocks"] = "الأجزاء ذات المحتوى الصفري",
        ["Decoded zero-content blocks"] = "الأجزاء ذات المحتوى الصفري بعد الفك",
        ["Zero-content note"] = "ملاحظة الأجزاء الصفرية",
        ["SHA256"] = "SHA256",
        ["Reconstructed SHA256"] = "SHA256 المعاد بناؤه",
        ["Issues"] = "المشاكل",
        ["Elapsed"] = "الوقت المستغرق",
        ["Throughput"] = "سرعة المعالجة",
        ["Repair needed"] = "هل يحتاج إصلاح",
        ["Conclusion"] = "الخلاصة",
        ["Limitations"] = "الحدود",
        ["Profile"] = "ملف الإعداد",
        ["Input format"] = "صيغة الإدخال",
        ["Repair mode"] = "وضع الإصلاح",
        ["Input verification"] = "فحص الإدخال",
        ["Output verification"] = "فحص الإخراج",
        ["Bytes read"] = "البايتات المقروءة",
        ["Bytes written"] = "البايتات المكتوبة",
        ["Padding bytes"] = "بايتات الحشو",
        ["Codec report blocks"] = "أجزاء تقرير codec",
        ["Raw image metadata"] = "بيانات الصورة الخام",
        ["Image format"] = "صيغة الصورة",
        ["Sector size"] = "حجم القطاع",
        ["Logical image size"] = "حجم الصورة المنطقي",
        ["Physical file size"] = "حجم الملف الفعلي",
        ["Payload read/decode"] = "قراءة/فك البيانات",
        ["Error"] = "الخطأ",
    };

    private static OperationOpenTarget? ResolveOpenTarget(OperationRequest request, string? reportPath)
    {
        if (ShouldPreferReportTarget(request.Kind) && IsExistingFile(reportPath))
        {
            return new OperationOpenTarget(reportPath!, IsReport: true);
        }

        if (request.Kind is UiOperationKind.Compress or UiOperationKind.Decompress && IsExistingFile(request.OutputPath))
        {
            return new OperationOpenTarget(request.OutputPath, IsReport: false);
        }

        if (IsExistingFile(reportPath))
        {
            return new OperationOpenTarget(reportPath!, IsReport: true);
        }

        return null;
    }

    private static bool ShouldPreferReportTarget(UiOperationKind operationKind)
    {
        return operationKind is UiOperationKind.Verify or UiOperationKind.Repair or UiOperationKind.Detect or UiOperationKind.Analyze or UiOperationKind.Measure;
    }

    private static bool IsExistingFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    private string SimplifyDetailsForUser(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return string.Empty;
        }

        return details
            .Replace("Input:", $"{Text.Input}:", StringComparison.Ordinal)
            .Replace("Output:", $"{Text.Output}:", StringComparison.Ordinal)
            .Replace("Format:", "Format:", StringComparison.Ordinal)
            .Replace("Error:", $"{Text.Error}:", StringComparison.Ordinal)
            .Replace("Warnings:", "Warnings:", StringComparison.Ordinal)
            .Trim();
    }

    private void AddLog(string title, string message, string kind)
    {
        LogEntries.Add(new UiLogEntry(title, message, kind));
    }

    private string BuildOperationStartMessage(OperationRequest request, string operationName)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return $"{operationName}\n{Text.Input}: {request.InputPath}";
        }

        return $"{operationName}\n{Text.Input}: {request.InputPath}\n{Text.Output}: {request.OutputPath}";
    }

    private string GetOperationName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.Compress,
            UiOperationKind.Detect => Text.Detect,
            UiOperationKind.Analyze => Text.Analyze,
            UiOperationKind.Measure => Text.Measure,
            UiOperationKind.Verify => Text.Verify,
            UiOperationKind.Decompress => Text.Decompress,
            UiOperationKind.Repair => Text.Repair,
            _ => throw new ArgumentOutOfRangeException(nameof(operationKind), operationKind, Text.UnsupportedOperation),
        };
    }

    private string GetOperationDescription(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.CompressDescription,
            UiOperationKind.Detect => Text.DetectDescription,
            UiOperationKind.Analyze => Text.AnalyzeDescription,
            UiOperationKind.Measure => Text.MeasureDescription,
            UiOperationKind.Verify => Text.VerifyDescription,
            UiOperationKind.Decompress => Text.DecompressDescription,
            UiOperationKind.Repair => Text.RepairDescription,
            _ => Text.UnsupportedOperation,
        };
    }

    private bool SelectedOperationUsesProfile => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Measure;

    private bool SelectedOperationUsesBlockSize => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Measure;

    private bool SelectedOperationUsesWorkerCount => SelectedOperation is UiOperationKind.Compress;

    private bool SelectedOperationUsesDeepVerify => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Verify or UiOperationKind.Repair;

    private bool SelectedOperationCanToggleDeepVerify => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Verify;

    private bool SelectedOperationUsesCodecReport => SelectedOperation is UiOperationKind.Compress or UiOperationKind.Repair;

    private void OnOperationPresentationChanged()
    {
        ClearOpenTarget();
        OnPropertyChanged(nameof(RequiresOutputPath));
        OnPropertyChanged(nameof(SelectedOperationName));
        OnPropertyChanged(nameof(SelectedOperationDescription));
        OnPropertyChanged(nameof(OutputRequirementText));
        OnPropertyChanged(nameof(CompactOutputText));
        OnPropertyChanged(nameof(CanOpenOutput));
        RefreshTaskStatuses();
        OnOptionAvailabilityChanged();
    }

    private void ApplyOperationDefaults()
    {
        if (SelectedOperation is UiOperationKind.Verify or UiOperationKind.Repair)
        {
            DeepVerify = true;
        }

        if (SelectedOperation is UiOperationKind.Repair)
        {
            SelectedProfileName = "game-safe";
        }
    }

    private void OnOptionAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanBrowseOutput));
        OnPropertyChanged(nameof(CanOpenOutput));
        OnPropertyChanged(nameof(CanUseProfile));
        OnPropertyChanged(nameof(CanUseForceOverwrite));
        OnPropertyChanged(nameof(CanUseWorkerCount));
        OnPropertyChanged(nameof(CanUseBlockSize));
        OnPropertyChanged(nameof(CanUseDeepVerify));
        OnPropertyChanged(nameof(CanUseSha256));
        OnPropertyChanged(nameof(CanUseCodecReport));
    }

    private void SetOpenTarget(string path, bool isReport)
    {
        string normalizedPath = path.Trim();
        openTargetPath = normalizedPath;
        openTargetIsReport = isReport;
        canOpenOutput = File.Exists(normalizedPath) || Directory.Exists(normalizedPath);
        OnPropertyChanged(nameof(OpenTargetPath));
        OnPropertyChanged(nameof(OpenResultText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private void ClearOpenTarget()
    {
        openTargetPath = string.Empty;
        openTargetIsReport = true;
        canOpenOutput = false;
        OnPropertyChanged(nameof(OpenTargetPath));
        OnPropertyChanged(nameof(OpenResultText));
        OnPropertyChanged(nameof(CanOpenOutput));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetInputPathFromTask(UiTaskItem task)
    {
        outputPathWasEditedByUser = false;
        InputPath = task.Path;
    }

    private void RefreshTaskStatuses()
    {
        foreach (UiTaskItem task in Tasks)
        {
            task.SetStatus(CreateTaskStatusForCurrentOperation(task.Path));
        }
    }

    private string CreateTaskStatusForCurrentOperation(string path)
    {
        string kind = Directory.Exists(path)
            ? "Folder"
            : GetMediaKind(Path.GetExtension(path));
        string action = SelectedOperation switch
        {
            UiOperationKind.Compress => Text.ReadyToCompress,
            UiOperationKind.Decompress => Text.ReadyToExtractIso,
            UiOperationKind.Verify => Text.ReadyToVerify,
            UiOperationKind.Repair => Text.ReadyToRepair,
            UiOperationKind.Detect => Text.ReadyToDetect,
            UiOperationKind.Analyze => Text.ReadyToAnalyze,
            UiOperationKind.Measure => Text.ReadyToMeasure,
            _ => Text.Ready,
        };

        return $"{kind} · {action}";
    }

    private string CreatePreparingStageText(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => Text.PreparingDetectInput,
            UiOperationKind.Decompress => Text.PreparingReadHeader,
            UiOperationKind.Verify => Text.PreparingCheckIndex,
            UiOperationKind.Repair => Text.PreparingRepair,
            UiOperationKind.Detect => Text.PreparingReadHeader,
            UiOperationKind.Analyze => Text.PreparingAnalyzeImage,
            UiOperationKind.Measure => Text.PreparingMeasure,
            _ => Text.Running,
        };
    }

    private string CreateProgressStageText(UiOperationKind operationKind, double progress)
    {
        if (progress < 8)
        {
            return Text.ProgressReadHeader;
        }

        if (progress < 24)
        {
            return Text.ProgressCheckIndex;
        }

        if (progress < 92)
        {
            return operationKind switch
            {
                UiOperationKind.Compress => Text.ProgressWriteCso,
                UiOperationKind.Decompress => Text.ProgressWriteIso,
                UiOperationKind.Repair => Text.ProgressRepair,
                UiOperationKind.Verify => Text.ProgressVerifyData,
                UiOperationKind.Measure => Text.ProgressMeasure,
                _ => Text.ProgressRunningOperation,
            };
        }

        return Text.ProgressCheckingOutput;
    }

    private string CreateCompletedStageText(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Repair => Text.RepairCompleted,
            UiOperationKind.Verify => Text.VerifyCompleted,
            UiOperationKind.Compress or UiOperationKind.Decompress => Text.WriteCompleted,
            _ => Text.Completed,
        };
    }

    private void RefreshLocalizedStatusText()
    {
        if (IsBusy)
        {
            return;
        }

        if (!hasCompletedOperation)
        {
            StatusText = Text.Ready;
            CurrentStageText = Text.Ready;
            return;
        }

        StatusText = lastCompletedOperationSucceeded ? Text.OperationSucceeded : Text.OperationFailed;
        CurrentStageText = lastCompletedOperationSucceeded && lastCompletedOperationKind is { } operationKind
            ? CreateCompletedStageText(operationKind)
            : Text.OperationFailed;
    }

    private static string GetMediaKind(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".iso" => "ISO",
            ".cso" => "CSO",
            ".pkg" => "PKG",
            ".chd" => "CHD",
            ".cue" => "CUE",
            ".bin" => "BIN",
            ".gdi" => "GDI",
            "" => "Unknown",
            _ => "Other",
        };
    }

    private readonly record struct OperationOpenTarget(string Path, bool IsReport);

    private sealed record OperationRequest(
        UiOperationKind Kind,
        string InputPath,
        string OutputPath,
        CsoCompressionProfile Profile,
        uint BlockSize,
        int WorkerCount,
        bool ForceOverwrite,
        bool DeepVerify,
        bool ComputeSha256,
        bool CodecReport);
}
