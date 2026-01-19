using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extensions;
using LyuOnnxCore.Extensions;
using LyuOnnxCore.Helpers;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace MahTemp.ViewModels;

public partial class DetectionViewModel : ViewModelBase
{
    public DetectionViewModel()
    {
        OnnxModelHelper.GetOnnxModels("OnnxModel").ForEach(o => OnnxSources.Add(o));
        SelectedOnnxModel = OnnxSources.FirstOrDefault();
        SelelctedLabels.CollectionChanged += SelelctedLabels_CollectionChanged;
    }

    #region Onnx

    public ObservableCollection<OnnxModelInfo> OnnxSources { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDetectionCommand))]
    public partial OnnxModelInfo? SelectedOnnxModel { get; set; }

    public ObservableCollection<string> LabesSource { get; } = [];

    public ObservableCollection<string> SelelctedLabels { get; } = [];

    [ObservableProperty]
    public partial double ConfidenceThreshold { get; set; } = 0.4;

    #region 检测设置

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

    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OriginalImage))]
    [NotifyCanExecuteChangedFor(nameof(StartDetectionCommand))]
    public partial string? ImagePath { get; set; }

    partial void OnImagePathChanged(string? value)
    {
        ResultImage = null;
    }

    public BitmapImage? OriginalImage =>
        ImagePath.IsNullOrWhiteSpace() ? null : new(new Uri(ImagePath));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCroppedRegionsCommand))]
    public partial BitmapSource? ResultImage { get; set; }

    private List<DetectionResult> _detectionResults = [];

    private void SelelctedLabels_CollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e
    )
    {
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void LoadLabesFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择标签文件",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            DefaultExt = ".txt",
        };

        if (dialog.ShowDialog() == true)
        {
            LabesSource.Clear();
            var labels = LabelHelper.LoadLabelsFromFile(dialog.FileName);
            labels.ForEach(l =>
            {
                LabesSource.Add(l);
                SelelctedLabels.Add(l);
            });
        }
    }

    [RelayCommand]
    private void LoadImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图片",
            Filter =
                "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
            ResultImage = null;
        }
    }

    [ObservableProperty]
    private bool isObbModel;

    [ObservableProperty]
    private bool isDetecting;

    [RelayCommand(CanExecute = nameof(CanStartDetection))]
    private async Task StartDetection()
    {
        IsDetecting = true;
        try
        {
            await Task.Run(() =>
            {
                using var session = new InferenceSession(SelectedOnnxModel!.FullPath);
                using var image = Cv2.ImRead(ImagePath!);

                if (image.Empty())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowMessage("图像加载失败", "错误");
                    });
                    return;
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
                };

                Mat resultMat;
                List<DetectionResult> detections;
                if (IsObbModel)
                {
                    detections = session.DetectOBB(
                        image,
                        [.. LabesSource],
                        detectionOptions
                    );
                    resultMat = image.DrawOBBDetections(detections, drawOptions);
                }
                else
                {
                    detections = session.Detect(
                        image,
                        [.. LabesSource],
                        detectionOptions
                    );
                    resultMat = image.DrawDetections(detections, drawOptions);
                }
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _detectionResults = detections;
                    ResultImage = resultMat.ToBitmapSource();
                    SaveCroppedRegionsCommand.NotifyCanExecuteChanged();
                });
                resultMat.Dispose();
            });
        }
        catch (Exception ex)
        {
            ShowMessage($"检测失败: {ex.Message}\n\n{ex.StackTrace}", "错误");
        }
        finally
        {
            IsDetecting = false;
        }
    }

    private bool CanStartDetection() =>
        !ImagePath.IsNullOrWhiteSpace()
        && SelectedOnnxModel is not null
        && SelelctedLabels.Count > 0;

    [RelayCommand(CanExecute = nameof(CanSaveCroppedRegions))]
    private void SaveCroppedRegions()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择保存裁剪区域的文件夹",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                using var image = Cv2.ImRead(ImagePath!);
                
                if (image.Empty())
                {
                    ShowMessage("无法加载原始图像", "错误");
                    return;
                }

                var savedFiles = image.SaveCroppedRegions(_detectionResults, dialog.FolderName, out var errorMessages);

                string message = $"检测到 {_detectionResults.Count} 个目标\n";
                message += $"成功保存 {savedFiles.Count} 个裁剪区域\n";
                
                if (errorMessages.Count > 0)
                {
                    message += $"\n失败 {errorMessages.Count} 个:\n";
                    message += string.Join("\n", errorMessages);                    
                }
                
                message += $"\n\n保存位置:\n{dialog.FolderName}";

                if (savedFiles.Count > 0)
                {
                    ShowMessage(message, errorMessages.Count > 0 ? "部分成功" : "成功");
                }
                else
                {
                    ShowMessage("所有裁剪都失败了\n\n" + string.Join("\n", errorMessages), "错误");
                }
            }
        }
        catch (Exception ex)
        {
            ShowMessage($"保存失败: {ex.Message}", "错误");
        }
    }

    private bool CanSaveCroppedRegions() =>
        ResultImage is not null && _detectionResults.Count > 0;

    #endregion
}
