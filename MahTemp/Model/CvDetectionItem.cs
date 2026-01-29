using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MahTemp.Extension;
using OpenCvSharp;

namespace MahTemp.Model;

public partial class CvDetectionItem : ObservableObject
{
    public CvDetectionItem()
    {
        SubscribeToSubSettings(CvSetting);
        CvSetting.PropertyChanged += OnCvSettingPropertyChanged;
    }

    [ObservableProperty]
    public partial int Index { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial Mat? PreviousMat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultMat))]
    public partial CvSettings CvSetting { get; set; } = new();

    partial void OnCvSettingChanged(CvSettings oldValue, CvSettings newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnCvSettingPropertyChanged;
            UnsubscribeFromSubSettings(oldValue);
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnCvSettingPropertyChanged;
            SubscribeToSubSettings(newValue);
        }
    }

    private void SubscribeToSubSettings(CvSettings setting)
    {
        if (setting.GaussianBlur != null)
            setting.GaussianBlur.PropertyChanged += OnSubSettingPropertyChanged;

        if (setting.Threshold != null)
            setting.Threshold.PropertyChanged += OnSubSettingPropertyChanged;

        if (setting.FindContours != null)
            setting.FindContours.PropertyChanged += OnSubSettingPropertyChanged;

        if (setting.DrawContours != null)
            setting.DrawContours.PropertyChanged += OnSubSettingPropertyChanged;

        setting.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(CvSettings.GaussianBlur))
            {
                if (setting.GaussianBlur != null)
                    setting.GaussianBlur.PropertyChanged += OnSubSettingPropertyChanged;
            }
            else if (e.PropertyName == nameof(CvSettings.Threshold))
            {
                if (setting.Threshold != null)
                    setting.Threshold.PropertyChanged += OnSubSettingPropertyChanged;
            }
            else if (e.PropertyName == nameof(CvSettings.FindContours))
            {
                if (setting.FindContours != null)
                    setting.FindContours.PropertyChanged += OnSubSettingPropertyChanged;
            }
            else if (e.PropertyName == nameof(CvSettings.DrawContours))
            {
                if (setting.DrawContours != null)
                    setting.DrawContours.PropertyChanged += OnSubSettingPropertyChanged;
            }
        };
    }

    private void UnsubscribeFromSubSettings(CvSettings setting)
    {
        if (setting.GaussianBlur != null)
            setting.GaussianBlur.PropertyChanged -= OnSubSettingPropertyChanged;

        if (setting.Threshold != null)
            setting.Threshold.PropertyChanged -= OnSubSettingPropertyChanged;

        if (setting.FindContours != null)
            setting.FindContours.PropertyChanged -= OnSubSettingPropertyChanged;

        if (setting.DrawContours != null)
            setting.DrawContours.PropertyChanged -= OnSubSettingPropertyChanged;
    }

    private void OnCvSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ResultMat));
    }

    private void OnSubSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ResultMat));
    }

    public Mat? ResultMat => PreviousMat?.GetResult(CvSetting);
}
