using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MahApps.Metro.Controls.Dialogs;
using MahTemp.Helper;

namespace MahTemp.ViewModels;

public class ViewModelBase : ObservableObject
{
    protected MessageDialogResult ShowMessage(
        string message,
        string title = "提示",
        MessageDialogStyle style = MessageDialogStyle.Affirmative)
        => DialogHelper.ShowMessageDialog(message, title, style);

    protected Task ShowCustomDialog<T>(T dialog) where T : BaseMetroDialog
        => DialogHelper.ShowCustomDialog(dialog);

    protected Task HideCustomDialog<T>(T dialog) where T : BaseMetroDialog
        => DialogHelper.HideCustomDialog(dialog);

    /// <summary>
    /// 发送消息
    /// </summary>
    protected void Send<TMessage>(TMessage message) where TMessage : class
        => WeakReferenceMessenger.Default.Send(message);

    /// <summary>
    /// 注册消息接收
    /// </summary>
    protected void Register<TMessage>(Action<object, TMessage> handler) where TMessage : class
        => WeakReferenceMessenger.Default.Register<TMessage>(this, (r, m) => handler(r, m));

    /// <summary>
    /// 取消注册
    /// </summary>
    protected void Unregister<TMessage>() where TMessage : class
        => WeakReferenceMessenger.Default.Unregister<TMessage>(this);

    /// <summary>
    /// 取消所有注册
    /// </summary>
    protected void UnregisterAll()
        => WeakReferenceMessenger.Default.UnregisterAll(this);
}
