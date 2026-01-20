using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extensions;
using LyuOnnxCore.Extensions;
using OpenCvSharp;

namespace MahTemp.ViewModels;

public partial class OpenCvViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviousImage))]
    public partial string ImagePath { get; set; } = string.Empty;

    public BitmapSource? PreviousImage =>
        ImagePath.IsNullOrEmpty() ? null : Cv2.ImRead(ImagePath).ToBitmapSource();

    [RelayCommand]
    private void LoadImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图片文件",
            Filter =
                "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
        }
    }
}
