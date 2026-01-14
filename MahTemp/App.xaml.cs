using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using LogExtension.Extensions;
using MahTemp.Services;
using MahTemp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace MahTemp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static ServiceProvider? Services { get; set; }

    private ILogger? _logger;

    #region 全局捕获

    private void App_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e
    )
    {
        // 处理UI线程异常
        _logger?.ZLogError(e.Exception, $"UI线程异常");
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // 处理非UI线程异常
        Exception? ex = e.ExceptionObject as Exception;
        _logger?.ZLogError(ex, $"非UI线程异常");
    }

    private void TaskScheduler_UnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e
    )
    {
        _logger?.ZLogError(e.Exception, $"任务异常");
        e.SetObserved(); // 标记异常已观察，防止程序崩溃
    }

    #endregion

    [MemberNotNull(nameof(Services))]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        #region 注册全局异常捕获

        // UI线程异常捕获
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // 非UI线程异常捕获
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 任务异常捕获
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        #endregion

        var services = new ServiceCollection();

        services.AddZLogger();

        services.RegisterViews();
        services.RegisterViewModels();

        Services = services.BuildServiceProvider();

        _logger = GetService<ILogger<App>>();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static T GetService<T>()
        where T : class
    {
        return Services!.GetService(typeof(T)) as T
            ?? throw new Exception("Cannot find service of specified type");
    }
}
