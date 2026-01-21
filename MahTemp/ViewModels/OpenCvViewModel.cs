using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Extensions;
using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.ViewModels;

public partial class OpenCvViewModel : ViewModelBase
{
    public OpenCvViewModel()
    {
        FlowItems.ListChanged += FlowItems_ListChanged;
    }

    private void FlowItems_ListChanged(object? sender, ListChangedEventArgs e)
    {
        switch (e.ListChangedType)
        {
            case ListChangedType.ItemAdded:
                if (e.NewIndex >= 0 && e.NewIndex < FlowItems.Count)
                {
                    var item = FlowItems[e.NewIndex];
                    if (e.NewIndex > 0)
                    {
                        item.PreviousMat = FlowItems[e.NewIndex - 1].ResultMat;
                    }
                }
                break;

            case ListChangedType.ItemDeleted:
                //nothing
                break;

            case ListChangedType.ItemChanged:
                if (e.NewIndex >= 0 && e.NewIndex < FlowItems.Count)
                {
                    var item = FlowItems[e.NewIndex];
                    if (e.PropertyDescriptor?.Name == nameof(CvDetectionItem.ResultMat))
                    {
                        if (e.NewIndex < FlowItems.Count - 1)
                        {
                            FlowItems[e.NewIndex + 1].PreviousMat = item.ResultMat;
                        }
                    }
                }
                break;

            case ListChangedType.Reset:
                //nothing
                break;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand))]
    public partial string ImagePath { get; set; } = string.Empty;

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
            FlowItems.Clear();
            FlowItems.Add(new CvDetectionItem { Index = 1, PreviousMat = Cv2.ImRead(ImagePath) });
        }
    }

    public BindingList<CvDetectionItem> FlowItems { get; } = [];

    [ObservableProperty]
    public partial CvDetectionItem? SelectedFlowItem { get; set; }

    private bool IsLoadImage() => !ImagePath.IsNullOrEmpty();

    [RelayCommand(CanExecute = nameof(IsLoadImage))]
    private void AddItem()
    {
        FlowItems.Add(new CvDetectionItem { Index = FlowItems.Count + 1 });
    }
}
