using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extensions;
using LyuCvExCore.Extensions;
using LyuOnnxCore.Helpers;
using LyuOnnxCore.Models;
using MahTemp.Enums;
using MahTemp.Helper;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace MahTemp.ViewModels;

public partial class DetectionViewModel : ViewModelBase
{
    public DetectionViewModel()
    {
        OnnxModelHelper.GetOnnxModels("OnnxModel").ForEach(o => OnnxSources.Add(o));
        SelectedOnnxModel = OnnxSources.FirstOrDefault();
        //LoadLabelsFromEnum();
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
    public partial double ConfidenceThreshold { get; set; } = 0.01;

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
    public partial BitmapSource? ResultImage { get; set; }

    private void SelelctedLabels_CollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e
    )
    {
        StartDetectionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void LoadLabelsFromEnum()
    {
        LabesSource.Clear();
        var labels = LabelHelper.GetLabelsFromEnum<DetectionLabel>();
        labels.ForEach(l => LabesSource.Add(l));
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
            labels.ForEach(l => LabesSource.Add(l));
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

    [RelayCommand(CanExecute = nameof(CanStartDetection))]
    private void StartDetection()
    {
        using var session = new InferenceSession(SelectedOnnxModel!.FullPath);
        using var image = Cv2.ImRead(ImagePath!);

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

        var resultMat = session.DetectAndDraw(
            image,
            [.. LabesSource],
            detectionOptions,
            drawOptions
        );
        ResultImage = resultMat.ToBitmapSource();
    }

    private bool CanStartDetection() =>
        !ImagePath.IsNullOrWhiteSpace()
        && SelectedOnnxModel is not null
        && SelelctedLabels.Count > 0;

    #endregion

    #region Cv

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CvOriginalImage))]
    [NotifyCanExecuteChangedFor(nameof(EdgeDetectionCommand))]
    public partial string CvImagePath { get; set; } = string.Empty;

    partial void OnCvImagePathChanged(string value)
    {
        CvResultImage = null;
    }

    public BitmapImage? CvOriginalImage =>
        CvImagePath.IsNullOrWhiteSpace() ? null : new(new Uri(CvImagePath));

    [ObservableProperty]
    public partial BitmapSource? CvResultImage { get; set; }

    #region 轮廓检测设置

    [ObservableProperty]
    public partial int ContourRetrievalMode { get; set; } = 0; // External

    [ObservableProperty]
    public partial int ContourApproximationMethod { get; set; } = 2; // ApproxSimple

    [ObservableProperty]
    public partial int GaussianBlurSize { get; set; } = 5;

    [ObservableProperty]
    public partial int ContourThresholdType { get; set; } = 2; // Otsu

    [ObservableProperty]
    public partial double ThresholdValue { get; set; } = 127;

    [ObservableProperty]
    public partial double MinArea { get; set; } = 100;

    [ObservableProperty]
    public partial double MaxArea { get; set; } = double.MaxValue;

    [ObservableProperty]
    public partial int MorphologyOperation { get; set; } = 7; // HitMiss (不进行形态学操作)

    [ObservableProperty]
    public partial int MorphologyKernelSize { get; set; } = 3;

    #endregion

    #region 轮廓绘制设置

    [ObservableProperty]
    public partial bool DrawContour { get; set; } = true;

    [ObservableProperty]
    public partial Color ContourColor { get; set; } = Colors.Green;

    [ObservableProperty]
    public partial int ContourThickness { get; set; } = 2;

    [ObservableProperty]
    public partial bool DrawBoundingRect { get; set; } = false;

    [ObservableProperty]
    public partial Color BoundingRectColor { get; set; } = Colors.Blue;

    [ObservableProperty]
    public partial int BoundingRectThickness { get; set; } = 2;

    [ObservableProperty]
    public partial bool DrawMinAreaRect { get; set; } = false;

    [ObservableProperty]
    public partial Color MinAreaRectColor { get; set; } = Colors.Red;

    [ObservableProperty]
    public partial int MinAreaRectThickness { get; set; } = 2;

    [ObservableProperty]
    public partial bool DrawCentroid { get; set; } = false;

    [ObservableProperty]
    public partial Color CentroidColor { get; set; } = Colors.Yellow;

    [ObservableProperty]
    public partial bool DrawConvexHull { get; set; } = false;

    [ObservableProperty]
    public partial Color ConvexHullColor { get; set; } = Colors.Cyan;

    [ObservableProperty]
    public partial int ConvexHullThickness { get; set; } = 1;

    #endregion

    [RelayCommand]
    private void LoadCvImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图片",
            Filter =
                "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            CvImagePath = dialog.FileName;
            CvResultImage = null;
        }
    }

    [RelayCommand(CanExecute = (nameof(CanDetection)))]
    private void EdgeDetection()
    {
        var pm = Cv2.ImRead(CvImagePath);
        
        var options = new ContourOptions
        {
            Mode = (RetrievalModes)ContourRetrievalMode,
            Method = (ContourApproximationModes)ContourApproximationMethod,
            GaussianBlurSize = GaussianBlurSize,
            ThresholdType = (ContourThresholdType)ContourThresholdType,
            ThresholdValue = ThresholdValue,
            MinArea = MinArea,
            MaxArea = MaxArea,
            MorphologyOperation = (MorphTypes)MorphologyOperation,
            MorphologyKernelSize = MorphologyKernelSize,
        };

        var drawOptions = new ContourDrawOptions
        {
            DrawContour = DrawContour,
            ContourColor = new Scalar(ContourColor.B, ContourColor.G, ContourColor.R),
            ContourThickness = ContourThickness,
            DrawBoundingRect = DrawBoundingRect,
            BoundingRectColor = new Scalar(BoundingRectColor.B, BoundingRectColor.G, BoundingRectColor.R),
            BoundingRectThickness = BoundingRectThickness,
            DrawMinAreaRect = DrawMinAreaRect,
            MinAreaRectColor = new Scalar(MinAreaRectColor.B, MinAreaRectColor.G, MinAreaRectColor.R),
            MinAreaRectThickness = MinAreaRectThickness,
            DrawCentroid = DrawCentroid,
            CentroidColor = new Scalar(CentroidColor.B, CentroidColor.G, CentroidColor.R),
            DrawConvexHull = DrawConvexHull,
            ConvexHullColor = new Scalar(ConvexHullColor.B, ConvexHullColor.G, ConvexHullColor.R),
            ConvexHullThickness = ConvexHullThickness,
        };
        
        var list = pm.FindContourInfos(options);
        CvResultImage = pm.DrawContourInfos(list, drawOptions).ToBitmapSource();
    }

    private bool CanDetection() => !CvImagePath.IsNullOrWhiteSpace();

    #endregion
}
