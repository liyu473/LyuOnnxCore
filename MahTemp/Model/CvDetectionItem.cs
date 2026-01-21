using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MahTemp.Extension;
using OpenCvSharp;

namespace MahTemp.Model;

public partial class CvDetectionItem : ObservableObject
{
    [ObservableProperty]
    public partial int Index { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial Mat? PreviousMat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial CvSettings? CvSetting { get; set; }

    partial void OnCvSettingChanged(CvSettings? oldValue, CvSettings? newValue)
    {
        oldValue?.PropertyChanged -= OnCvSettingChanged;
        newValue?.PropertyChanged += OnCvSettingChanged;
    }

    private void OnCvSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ResultMat));
    }

    public Mat? ResultMat => PreviousMat?.GetResult(CvSetting);
}
