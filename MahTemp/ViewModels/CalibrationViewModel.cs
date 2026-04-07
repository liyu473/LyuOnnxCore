using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyuOnnxCore.Calibration;
using Microsoft.Win32;
using OpenCvSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MahTemp.ViewModels;

public partial class CalibrationViewModel : ViewModelBase
{
    [ObservableProperty]
    private int innerCornerColumns = 9;

    [ObservableProperty]
    private int innerCornerRows = 6;

    [ObservableProperty]
    private double squareSize = 30.0;

    [ObservableProperty]
    private ObservableCollection<CalibrationImageItem> calibrationImages = [];

    [ObservableProperty]
    private CalibrationImageItem? selectedImage;

    [ObservableProperty]
    private BitmapImage? previewImage;

    [ObservableProperty]
    private bool isCalibrating;

    [ObservableProperty]
    private bool hasCalibrationResult;

    [ObservableProperty]
    private string imageSizeText = string.Empty;

    [ObservableProperty]
    private string reprojectionErrorText = string.Empty;

    [ObservableProperty]
    private string successfulImageCountText = string.Empty;

    [ObservableProperty]
    private string cameraMatrixText = string.Empty;

    [ObservableProperty]
    private string distortionCoefficientsText = string.Empty;

    private CameraCalibrationResult? _calibrationResult;

    public bool CanCalibrate => CalibrationImages.Count >= 3;

    public string ImageListHeader => $"标定图片列表 ({CalibrationImages.Count})";

    partial void OnSelectedImageChanged(CalibrationImageItem? value)
    {
        if (value != null)
        {
            LoadPreviewImage(value.FilePath, value.IsDetected);
        }
    }

    partial void OnCalibrationImagesChanged(ObservableCollection<CalibrationImageItem> value)
    {
        OnPropertyChanged(nameof(CanCalibrate));
        OnPropertyChanged(nameof(ImageListHeader));
    }

    [RelayCommand]
    private void LoadCalibrationImages()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp",
            Multiselect = true,
            Title = "选择标定图片"
        };

        if (dialog.ShowDialog() == true)
        {
            CalibrationImages.Clear();
            foreach (var filePath in dialog.FileNames)
            {
                CalibrationImages.Add(new CalibrationImageItem
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath)
                });
            }

            if (CalibrationImages.Count > 0)
            {
                SelectedImage = CalibrationImages[0];
            }

            OnPropertyChanged(nameof(CanCalibrate));
            OnPropertyChanged(nameof(ImageListHeader));
        }
    }

    [RelayCommand]
    private async Task StartCalibration()
    {
        if (CalibrationImages.Count < 3)
        {
            Helper.DialogHelper.ShowMessageDialog("至少需要3张标定图片", "错误");
            return;
        }

        IsCalibrating = true;
        HasCalibrationResult = false;

        try
        {
            await Task.Run(() =>
            {
                var board = new ChessboardCalibrationBoard(InnerCornerColumns, InnerCornerRows, SquareSize);
                var imagePaths = CalibrationImages.Select(x => x.FilePath).ToList();

                // 先检测每张图片的棋盘格
                foreach (var item in CalibrationImages)
                {
                    using var mat = Cv2.ImRead(item.FilePath);
                    var detection = CameraCalibrationService.DetectChessboardCorners(mat, board);
                    item.IsDetected = detection.IsSuccess && detection.Corners.Length == board.CornerCount;
                }

                // 执行标定
                _calibrationResult = CameraCalibrationService.CalibrateFromChessboardImageFiles(
                    imagePaths,
                    board);

                // 更新 UI
                App.Current.Dispatcher.Invoke(UpdateCalibrationResult);
            });

            Helper.DialogHelper.ShowMessageDialog("相机标定完成！", "成功");
        }
        catch (Exception ex)
        {
            Helper.DialogHelper.ShowMessageDialog($"标定失败：{ex.Message}", "错误");
        }
        finally
        {
            IsCalibrating = false;
        }
    }

    [RelayCommand]
    private void SaveCalibrationResult()
    {
        if (_calibrationResult == null)
        {
            Helper.DialogHelper.ShowMessageDialog("没有可保存的标定结果", "错误");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON文件|*.json",
            FileName = "calibration_result.json",
            Title = "保存标定结果"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    ImageSize = new { _calibrationResult.ImageSize.Width, _calibrationResult.ImageSize.Height },
                    CameraMatrix = _calibrationResult.CameraMatrix,
                    DistortionCoefficients = _calibrationResult.DistortionCoefficients,
                    ReprojectionError = _calibrationResult.ReprojectionError,
                    SuccessfulImageCount = _calibrationResult.SuccessfulImageCount,
                    InputImageCount = _calibrationResult.InputImageCount,
                    SkippedMismatchedResolutionCount = _calibrationResult.SkippedMismatchedResolutionCount
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(dialog.FileName, json);
                Helper.DialogHelper.ShowMessageDialog("标定结果已保存", "成功");
            }
            catch (Exception ex)
            {
                Helper.DialogHelper.ShowMessageDialog($"保存失败：{ex.Message}", "错误");
            }
        }
    }

    private void UpdateCalibrationResult()
    {
        if (_calibrationResult == null)
            return;

        HasCalibrationResult = true;
        ImageSizeText = $"{_calibrationResult.ImageSize.Width} x {_calibrationResult.ImageSize.Height}";
        ReprojectionErrorText = $"{_calibrationResult.ReprojectionError:F4} 像素";
        SuccessfulImageCountText = $"{_calibrationResult.SuccessfulImageCount} / {_calibrationResult.InputImageCount}";
        if (_calibrationResult.SkippedMismatchedResolutionCount > 0)
        {
            SuccessfulImageCountText += $"（已跳过 {_calibrationResult.SkippedMismatchedResolutionCount} 张分辨率不一致图片）";
        }

        var matrixBuilder = new StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            matrixBuilder.AppendLine($"[{_calibrationResult.CameraMatrix[i, 0]:F2}, {_calibrationResult.CameraMatrix[i, 1]:F2}, {_calibrationResult.CameraMatrix[i, 2]:F2}]");
        }
        CameraMatrixText = matrixBuilder.ToString();

        DistortionCoefficientsText = string.Join(", ", _calibrationResult.DistortionCoefficients.Select(x => x.ToString("F4")));
    }

    private void LoadPreviewImage(string filePath, bool drawCorners = false)
    {
        try
        {
            if (drawCorners)
            {
                // 绘制检测到的角点
                using var mat = Cv2.ImRead(filePath);
                var board = new ChessboardCalibrationBoard(InnerCornerColumns, InnerCornerRows, SquareSize);
                var detection = CameraCalibrationService.DetectChessboardCorners(mat, board);

                if (detection.IsSuccess && detection.Corners.Length > 0)
                {
                    Cv2.DrawChessboardCorners(mat, board.PatternSize, detection.Corners, detection.IsSuccess);
                }

                PreviewImage = MatToBitmapImage(mat);
            }
            else
            {
                // 显示原始图片
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewImage = bitmap;
            }
        }
        catch
        {
            PreviewImage = null;
        }
    }

    private static BitmapImage MatToBitmapImage(Mat mat)
    {
        using var ms = new MemoryStream();
        mat.WriteToStream(ms, ".png");
        ms.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}

public partial class CalibrationImageItem : ObservableObject
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private bool isDetected;
}
