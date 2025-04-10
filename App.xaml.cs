using System;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace HttpMonitoringSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        try
        {
            // Проверяем, что все необходимые сборки доступны
            CheckRequiredAssemblies();
            
            // Добавляем обработчик неперехваченных исключений
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            
            // Обработчик для неперехваченных исключений в других потоках
            Application.Current.Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        }
        catch (Exception ex)
        {
            // Показать детали ошибки на самом раннем этапе запуска
            MessageBox.Show($"Критическая ошибка при инициализации приложения: {ex.Message}\n\n" +
                $"Детали: {ex.StackTrace}", 
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CheckRequiredAssemblies()
    {
        try
        {
            // Проверяем наличие LiveCharts
            var liveChartsAssembly = Assembly.Load("LiveCharts");
            var liveChartsWpfAssembly = Assembly.Load("LiveCharts.Wpf");
            
            // Проверяем наличие Newtonsoft.Json
            var newtonsoftJsonAssembly = Assembly.Load("Newtonsoft.Json");
            
            // Логируем информацию в консоль
            Console.WriteLine($"LiveCharts: {liveChartsAssembly.GetName().Version}");
            Console.WriteLine($"LiveCharts.Wpf: {liveChartsWpfAssembly.GetName().Version}");
            Console.WriteLine($"Newtonsoft.Json: {newtonsoftJsonAssembly.GetName().Version}");
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Не удалось загрузить одну из необходимых библиотек: {ex.Message}", ex);
        }
    }

    private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowErrorMessage("Необработанное исключение в UI потоке (Dispatcher)", e.Exception);
        e.Handled = true;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowErrorMessage("Необработанное исключение в UI потоке", e.Exception);
        e.Handled = true; // Предотвращаем завершение приложения
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ShowErrorMessage("Критическая ошибка приложения", e.ExceptionObject as Exception);
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowErrorMessage("Необработанное исключение в Task", e.Exception);
        e.SetObserved(); // Помечаем исключение как обработанное
    }

    private void ShowErrorMessage(string title, Exception ex)
    {
        string errorMessage = "Неизвестная ошибка";
        
        if (ex != null)
        {
            errorMessage = $"Сообщение: {ex.Message}\n\n";
            
            if (ex.InnerException != null)
            {
                errorMessage += $"Внутреннее исключение: {ex.InnerException.Message}\n\n";
            }
            
            errorMessage += $"Стек вызовов:\n{ex.StackTrace}";
            
            // Записываем подробную информацию об ошибке в файл
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {title}: {errorMessage}\n\n");
            }
            catch
            {
                // Игнорируем ошибки при записи лога
            }
        }

        // Безопасный вызов MessageBox из любого потока
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            MessageBox.Show(
                errorMessage,
                $"Ошибка: {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }
}

