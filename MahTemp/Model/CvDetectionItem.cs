using CommunityToolkit.Mvvm.ComponentModel;
using MahTemp.Extension;
using OpenCvSharp;

namespace MahTemp.Model;

public partial class CvDetectionItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial Mat? PreviousMat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial CvSettings? CvSetting { get; set; }

    public Mat? ResultMat =>
        (PreviousMat == null || CvSetting == null) ? null : PreviousMat.GetResult(CvSetting);
}
