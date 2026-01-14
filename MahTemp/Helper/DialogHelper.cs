using System.Windows;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahTemp.Views;
using WpfWindow = System.Windows.Window;

namespace MahTemp.Helper;

public static class DialogHelper
{
    private static MainWindow Window => App.GetService<MainWindow>();

    /// <summary>
    /// 弹窗辅助
    /// </summary>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="style">仅支持 0，1</param>
    /// <returns></returns>
    public static MessageDialogResult ShowMessageDialog(
        string message,
        string title = "提示",
        MessageDialogStyle style = MessageDialogStyle.Affirmative
    )
    {
        MetroDialogSettings dialogSettings;
        dialogSettings =
            style == MessageDialogStyle.Affirmative
                ? new MetroDialogSettings(Window.MetroDialogOptions)
                {
                    AffirmativeButtonText = "OK",
                    ColorScheme = Window.MetroDialogOptions!.ColorScheme,
                }
                : new MetroDialogSettings(Window.MetroDialogOptions)
                {
                    AffirmativeButtonText = "OK",
                    NegativeButtonText = "Cancel",
                    DefaultButtonFocus = MessageDialogResult.Affirmative,
                    ColorScheme = Window.MetroDialogOptions!.ColorScheme,
                };

        return Window.ShowModalMessageExternal(title, message, style, dialogSettings);
    }

    /// <summary>
    /// 页面元素弹窗辅助
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="element"></param>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="style"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static MessageDialogResult ShowMessageDialog<T>(
        this T element,
        string message,
        string title = "提示",
        MessageDialogStyle style = MessageDialogStyle.Affirmative
    )
        where T : FrameworkElement
    {
        if (WpfWindow.GetWindow(element) is not MetroWindow window)
            throw new InvalidOperationException("无法找到 MetroWindow");

        MetroDialogSettings dialogSettings;
        dialogSettings =
            style == MessageDialogStyle.Affirmative
                ? new MetroDialogSettings(Window.MetroDialogOptions)
                {
                    AffirmativeButtonText = "OK",
                    ColorScheme = Window.MetroDialogOptions!.ColorScheme,
                }
                : new MetroDialogSettings(Window.MetroDialogOptions)
                {
                    AffirmativeButtonText = "OK",
                    NegativeButtonText = "Cancel",
                    DefaultButtonFocus = MessageDialogResult.Affirmative,
                    ColorScheme = Window.MetroDialogOptions!.ColorScheme,
                };

        return window.ShowModalMessageExternal(title, message, style, dialogSettings);
    }

    /// <summary>
    /// 展示自定义对话框
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dialog"></param>
    /// <returns></returns>
    public static async Task ShowCustomDialog<T>(T dialog)
        where T : BaseMetroDialog
    {
        await Window.ShowMetroDialogAsync(dialog);
    }

    /// <summary>
    /// 隐藏自定义对话框
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dialog"></param>
    /// <returns></returns>
    public static async Task HideCustomDialog<T>(T dialog)
        where T : BaseMetroDialog
    {
        await Window.HideMetroDialogAsync(dialog);
    }
}
