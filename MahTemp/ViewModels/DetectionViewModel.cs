using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyuOnnxCore.Extensions;
using LyuOnnxCore.Helpers;
using LyuOnnxCore.Interfaces;
using LyuOnnxCore.Models;
using MahTemp.Extension;
using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.ViewModels;

public partial class DetectionViewModel : ViewModelBase
{
    private const string OnnxModelFolder = "OnnxModel";

    private readonly IYoloHbbDetectionService _yoloHbbDetectionService;
    private readonly IYoloObbDetectionService _yoloObbDetectionService;
    private readonly IYoloXHbbDetectionService _yoloXHbbDetectionService;
    private IReadOnlyList<HbbDetectionResult> _hbbDetectionResults = [];
    private IReadOnlyList<ObbDetectionResult> _obbDetectionResults = [];
    private bool _lastDetectionWasObb;

    public DetectionViewModel(
        IYoloHbbDetectionService yoloHbbDetectionService,
        IYoloObbDetectionService yoloObbDetectionService,
        IYoloXHbbDetectionService yoloXHbbDetectionService
    )
    {
        _yoloHbbDetectionService = yoloHbbDetectionService;
        _yoloObbDetectionService = yoloObbDetectionService;
        _yoloXHbbDetectionService = yoloXHbbDetectionService;

        SelelctedLabels.CollectionChanged += SelelctedLabels_CollectionChanged;

        DetectionModes.Add(
            new DetectionModeOption(
                DetectionMode.YoloXHbb,
                "YOLOX HBB",
                "Use the YOLOX decoder for horizontal bounding box detection."
            )
        );
        DetectionModes.Add(
            new DetectionModeOption(
                DetectionMode.YoloHbb,
                "YOLO HBB",
                "Use the standard YOLO head for horizontal bounding box detection."
            )
        );
        DetectionModes.Add(
            new DetectionModeOption(
                DetectionMode.YoloObb,
                "YOLO OBB",
                "Use the rotated-box pipeline for oriented object detection."
            )
        );

        SelectedDetectionMode = DetectionModes.First();
        RefreshModels();
    }

    public ObservableCollection<OnnxModelInfo> OnnxSources { get; } = [];

    public ObservableCollection<string> LabesSource { get; } = [];

    public ObservableCollection<string> SelelctedLabels { get; } = [];

    public ObservableCollection<DetectionModeOption> DetectionModes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDetectionCommand))]
    public partial OnnxModelInfo? SelectedOnnxModel { get; set; }

    partial void OnSelectedOnnxModelChanged(OnnxModelInfo? value)
    {
        TryLoadLabelsForModel(value);
        ClearDetectionState(keepImagePath: true);
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDetectionCommand))]
    public partial DetectionModeOption? SelectedDetectionMode { get; set; }

    partial void OnSelectedDetectionModeChanged(DetectionModeOption? value)
    {
        DetectionSummary = value is null
            ? null
            : $"{value.DisplayName}: {value.Description}";
        ClearDetectionState(keepImagePath: true);
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    public partial double ConfidenceThreshold { get; set; } = 0.4;

    [ObservableProperty]
    public partial double NmsThreshold { get; set; } = 0.45;

    [ObservableProperty]
    public partial bool ShowConfidence { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowLabel { get; set; } = true;

    [ObservableProperty]
    public partial int BoxThickness { get; set; } = 2;

    [ObservableProperty]
    public partial double FontScale { get; set; } = 0.5;

    [ObservableProperty]
    public partial Color BoxColor { get; set; } = Colors.Green;

    [ObservableProperty]
    public partial Color TextColor { get; set; } = Colors.Yellow;

    [ObservableProperty]
    public partial bool IsFilterOverlay { get; set; } = true;

    [ObservableProperty]
    public partial bool IsCrossClass { get; set; } = true;

    [ObservableProperty]
    public partial double OverlayThreshold { get; set; } = 0.8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OriginalImage))]
    [NotifyCanExecuteChangedFor(nameof(StartDetectionCommand))]
    public partial string? ImagePath { get; set; }

    partial void OnImagePathChanged(string? value)
    {
        ClearDetectionState(keepImagePath: true);
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    public BitmapImage? OriginalImage =>
        string.IsNullOrWhiteSpace(ImagePath) ? null : new BitmapImage(new Uri(ImagePath));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCroppedRegionsCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveDetectionResultCommand))]
    public partial BitmapSource? ResultImage { get; set; }

    [ObservableProperty]
    public partial bool IsDetecting { get; set; }

    [ObservableProperty]
    public partial string? DetectionTime { get; set; }

    [ObservableProperty]
    public partial string? DetectionSummary { get; set; }

    [RelayCommand]
    private void RefreshModels()
    {
        string? selectedModelPath = SelectedOnnxModel?.FullPath;
        OnnxSources.Clear();

        try
        {
            foreach (
                var model in OnnxModelHelper.GetOnnxModels(OnnxModelFolder, SearchOption.AllDirectories)
            )
            {
                OnnxSources.Add(model);
            }
        }
        catch (DirectoryNotFoundException)
        {
            DetectionSummary = $"Model folder '{OnnxModelFolder}' was not found.";
            SelectedOnnxModel = null;
            return;
        }

        SelectedOnnxModel =
            OnnxSources.FirstOrDefault(model => model.FullPath == selectedModelPath)
            ?? OnnxSources.FirstOrDefault();
    }

    [RelayCommand]
    private void LoadLabesFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select labels file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
        };

        if (dialog.ShowDialog() == true)
        {
            LoadLabelsFromPath(dialog.FileName);
        }
    }

    [RelayCommand]
    private void LoadImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select image",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartDetection))]
    private async Task StartDetection()
    {
        IsDetecting = true;
        DetectionTime = null;

        try
        {
            if (SelectedOnnxModel is null || SelectedDetectionMode is null)
            {
                return;
            }

            using var image = Cv2.ImRead(ImagePath!);
            if (image.Empty())
            {
                throw new InvalidOperationException("Failed to load the selected image.");
            }

            var detectionOptions = new DetectionOptions
            {
                ConfidenceThreshold = (float)ConfidenceThreshold,
                NmsThreshold = (float)NmsThreshold,
                FilterLabels = [.. SelelctedLabels],
                IsFilterOverlay = IsFilterOverlay,
                IsCrossClass = IsCrossClass,
                OverlayThreshold = (float)OverlayThreshold,
            };

            var drawOptions = new DrawOptions
            {
                ShowConfidence = ShowConfidence,
                ShowLabel = ShowLabel,
                BoxThickness = BoxThickness,
                FontScale = FontScale,
                BoxColor = (BoxColor.B, BoxColor.G, BoxColor.R),
                TextColor = (TextColor.B, TextColor.G, TextColor.R),
                UseChineseFont = false,
            };

            var labels = LabesSource.ToArray();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var detectionResult = await Task.Run(() =>
                DetectObjects(
                    SelectedDetectionMode.Mode,
                    SelectedOnnxModel.FullPath,
                    image,
                    labels,
                    detectionOptions,
                    drawOptions
                )
            );

            stopwatch.Stop();

            ResultImage = detectionResult.DrawnImage.ToBitmapSource();
            detectionResult.DrawnImage.Dispose();
            _hbbDetectionResults = detectionResult.HbbResults;
            _obbDetectionResults = detectionResult.ObbResults;
            _lastDetectionWasObb = detectionResult.IsObb;

            DetectionTime = $"Elapsed: {stopwatch.ElapsedMilliseconds} ms";
            DetectionSummary =
                $"{SelectedDetectionMode.DisplayName}: detected {detectionResult.Count} object(s)";

            SaveCroppedRegionsCommand.NotifyCanExecuteChanged();
            SaveDetectionResultCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ShowMessage($"Detection failed: {ex.Message}\n\n{ex.StackTrace}", "Error");
        }
        finally
        {
            IsDetecting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveCroppedRegions))]
    private void SaveCroppedRegions()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select output folder",
                Multiselect = false,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            using var image = Cv2.ImRead(ImagePath!);
            if (image.Empty())
            {
                ShowMessage("Failed to load the original image.", "Error");
                return;
            }

            if (_lastDetectionWasObb)
            {
                var savedFiles = image.SaveCroppedRegions(
                    _obbDetectionResults,
                    dialog.FolderName,
                    out var errorMessages
                );
                ShowCropSaveMessage(
                    dialog.FolderName,
                    _obbDetectionResults.Count,
                    savedFiles,
                    errorMessages
                );
                return;
            }

            var hbbSavedFiles = image.SaveCroppedRegions(
                _hbbDetectionResults,
                dialog.FolderName,
                out var hbbErrorMessages
            );
            ShowCropSaveMessage(
                dialog.FolderName,
                _hbbDetectionResults.Count,
                hbbSavedFiles,
                hbbErrorMessages
            );
        }
        catch (Exception ex)
        {
            ShowMessage($"Save failed: {ex.Message}", "Error");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveDetectionResult))]
    private void SaveDetectionResult()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save detection result",
                Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP image (*.bmp)|*.bmp|All files (*.*)|*.*",
                DefaultExt = ".png",
                FileName = $"detection_result_{DateTime.Now:yyyyMMdd_HHmmss}",
            };

            if (dialog.ShowDialog() == true)
            {
                using var resultMat = ResultImage!.ToMat();
                Cv2.ImWrite(dialog.FileName, resultMat);

                ShowMessage(
                    $"Saved detection result to:\n{dialog.FileName}\n\nDetected {CurrentDetectionCount} object(s).",
                    "Success"
                );
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"Save failed: {ex.Message}", "Error");
        }
    }

    private bool CanStartDetection() =>
        !string.IsNullOrWhiteSpace(ImagePath)
        && SelectedOnnxModel is not null
        && SelectedDetectionMode is not null
        && SelelctedLabels.Count > 0;

    private bool CanSaveCroppedRegions() =>
        ResultImage is not null && CurrentDetectionCount > 0;

    private bool CanSaveDetectionResult() =>
        ResultImage is not null && CurrentDetectionCount > 0;

    private int CurrentDetectionCount =>
        _lastDetectionWasObb ? _obbDetectionResults.Count : _hbbDetectionResults.Count;

    private void SelelctedLabels_CollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e
    )
    {
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    private void LoadLabelsFromPath(string labelFilePath)
    {
        LabesSource.Clear();
        SelelctedLabels.Clear();

        foreach (var label in LabelHelper.LoadLabelsFromFile(labelFilePath))
        {
            LabesSource.Add(label);
            SelelctedLabels.Add(label);
        }
    }

    private void TryLoadLabelsForModel(OnnxModelInfo? model)
    {
        if (model is null)
        {
            return;
        }

        string modelDirectory = Path.GetDirectoryName(model.FullPath) ?? string.Empty;
        string[] labelCandidates =
        [
            Path.Combine(modelDirectory, "classes.txt"),
            Path.Combine(OnnxModelFolder, "classes.txt"),
        ];

        string? labelPath = labelCandidates.FirstOrDefault(File.Exists);
        if (labelPath is not null)
        {
            LoadLabelsFromPath(labelPath);
        }
    }

    private void ClearDetectionState(bool keepImagePath)
    {
        ResultImage = null;
        DetectionTime = null;
        _hbbDetectionResults = [];
        _obbDetectionResults = [];
        _lastDetectionWasObb = false;

        if (!keepImagePath)
        {
            ImagePath = null;
        }

        SaveCroppedRegionsCommand.NotifyCanExecuteChanged();
        SaveDetectionResultCommand.NotifyCanExecuteChanged();
    }

    private DetectionExecutionResult DetectObjects(
        DetectionMode mode,
        string modelPath,
        Mat image,
        string[] labels,
        DetectionOptions detectionOptions,
        DrawOptions drawOptions
    )
    {
        switch (mode)
        {
            case DetectionMode.YoloObb:
            {
                var results = _yoloObbDetectionService.Detect(
                    modelPath,
                    image,
                    labels,
                    detectionOptions
                );
                var drawnImage = image.DrawOBBDetections(results, drawOptions);
                return new DetectionExecutionResult(drawnImage, [], results, true);
            }

            case DetectionMode.YoloHbb:
            {
                var results = _yoloHbbDetectionService.Detect(
                    modelPath,
                    image,
                    labels,
                    detectionOptions
                );
                var drawnImage = image.DrawDetections(results, drawOptions);
                return new DetectionExecutionResult(drawnImage, results, [], false);
            }

            default:
            {
                var results = _yoloXHbbDetectionService.Detect(
                    modelPath,
                    image,
                    labels,
                    detectionOptions
                );
                var drawnImage = image.DrawDetections(results, drawOptions);
                return new DetectionExecutionResult(drawnImage, results, [], false);
            }
        }
    }

    private void ShowCropSaveMessage(
        string folderName,
        int detectionCount,
        IReadOnlyList<string> savedFiles,
        IReadOnlyList<string> errorMessages
    )
    {
        string message = $"Detected {detectionCount} object(s).\n";
        message += $"Saved {savedFiles.Count} cropped region(s).";

        if (errorMessages.Count > 0)
        {
            message += $"\n\nFailed: {errorMessages.Count}\n";
            message += string.Join("\n", errorMessages);
        }

        message += $"\n\nOutput folder:\n{folderName}";

        if (savedFiles.Count > 0)
        {
            ShowMessage(message, errorMessages.Count > 0 ? "Partial Success" : "Success");
        }
        else
        {
            ShowMessage(
                "All cropped regions failed to save.\n\n" + string.Join("\n", errorMessages),
                "Error"
            );
        }
    }

    private sealed record DetectionExecutionResult(
        Mat DrawnImage,
        IReadOnlyList<HbbDetectionResult> HbbResults,
        IReadOnlyList<ObbDetectionResult> ObbResults,
        bool IsObb
    )
    {
        public int Count => IsObb ? ObbResults.Count : HbbResults.Count;
    }
}
