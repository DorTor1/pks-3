using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HttpMonitoringSystem.Models;
using HttpMonitoringSystem.Services;
using HttpMonitoringSystem.ViewModels;

namespace HttpMonitoringSystem;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private HttpServerService _serverService;
    private HttpClientService _clientService;
    private StatisticsService _statisticsService;
    private ChartViewModel _chartViewModel;
    private ObservableCollection<RequestModel> _requests;
    private bool _isChartsInitialized = false;

    public MainWindow()
    {
        try
        {
            // Сначала инициализируем интерфейс без связывания данных
            InitializeComponent();
            
            // Затем инициализируем сервисы
            InitializeServices();
            
            // Попытаемся инициализировать UI с данными
            InitializeUIWithoutCharts();
            
            // Показываем сообщение в логах для отладки
            LogDebugMessage("Приложение успешно инициализировано");
            
            // Устанавливаем порт по умолчанию выше 1024 (не требует прав администратора)
            ServerPortTextBox.Text = "8080";
            
            // Инициализируем графики отдельно
            try
            {
                InitializeCharts();
                _isChartsInitialized = true;
            }
            catch (Exception chartEx)
            {
                LogDebugMessage($"Предупреждение: Не удалось инициализировать графики: {chartEx.Message}. " +
                    "Функциональность графиков будет отключена.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при инициализации приложения: {ex.Message}\n\nСтек вызовов: {ex.StackTrace}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LogDebugMessage(string message)
    {
        try
        {
            AddToServerLogs(message);
            System.Diagnostics.Debug.WriteLine(message);
        }
        catch
        {
            // Игнорируем ошибки при логировании
        }
    }

    private void InitializeServices()
    {
        _serverService = new HttpServerService();
        _clientService = new HttpClientService();
        _statisticsService = new StatisticsService(_serverService);
        
        _serverService.LogMessage += (s, e) => AddToServerLogs(e);
        _serverService.RequestReceived += (s, e) => Dispatcher.Invoke(() => UpdateRequestsList());
        
        _clientService.LogMessage += (s, e) => AddToServerLogs(e);
        _clientService.RequestSent += (s, e) => 
        {
            _statisticsService.AddRequest(e);
            Dispatcher.Invoke(() => UpdateRequestsList());
        };
        
        _requests = new ObservableCollection<RequestModel>();
    }

    private void InitializeUIWithoutCharts()
    {
        // Привязываем коллекцию запросов к DataGrid
        RequestsDataGrid.ItemsSource = _requests;
        
        // Инициализация клиентской части - УСТАНАВЛИВАЕМ НАЧАЛЬНОЕ СОСТОЯНИЕ НАПРЯМУЮ
        // ClientMethodComboBox_SelectionChanged(null, null); // Убираем этот вызов
        // Устанавливаем начальное состояние для клиентской части
        if (ClientMethodComboBox != null && ClientMethodComboBox.Items.Count > 0)
        {
             // Выбираем GET по умолчанию, если возможно
            ClientMethodComboBox.SelectedIndex = 0; 
        }
        // Поле тела запроса изначально неактивно (т.к. GET выбран)
        if (ClientRequestBodyTextBox != null) 
        {
            ClientRequestBodyTextBox.IsEnabled = false;
        }
    }
    
    private void InitializeCharts()
    {
        _chartViewModel = new ChartViewModel(_statisticsService);
        DataContext = _chartViewModel;
    }

    private void AddToServerLogs(string message)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                ServerLogsTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                ServerLogsTextBox.ScrollToEnd();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка при добавлении в лог: {ex.Message}");
        }
    }

    private void UpdateRequestsList()
    {
        try
        {
            _requests.Clear();
            var filteredRequests = FilterRequests();
            foreach (var request in filteredRequests)
            {
                _requests.Add(request);
            }
            
            UpdateStatisticsLabels();
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при обновлении списка запросов: {ex.Message}");
        }
    }

    private IEnumerable<RequestModel> FilterRequests()
    {
        var query = _statisticsService.GetAllRequests().AsEnumerable(); // Получаем все запросы из StatisticsService

        // Фильтр по методу
        if (MethodFilterComboBox?.SelectedItem is ComboBoxItem methodItem && methodItem.Content.ToString() != "Все")
        {
            string selectedMethod = methodItem.Content.ToString();
            query = query.Where(r => r.Method.Equals(selectedMethod, StringComparison.OrdinalIgnoreCase));
        }

        // Фильтр по статусу
        if (StatusFilterComboBox?.SelectedItem is ComboBoxItem statusItem && statusItem.Content.ToString() != "Все")
        {
            string selectedStatus = statusItem.Content.ToString();
            if (int.TryParse(selectedStatus.Split(' ')[0], out int statusCode))
            {
                query = query.Where(r => r.StatusCode == statusCode);
            }
            else if (selectedStatus == "Ошибки (4xx/5xx)")
            {
                query = query.Where(r => r.StatusCode >= 400);
            }
            else if (selectedStatus == "Успешные (2xx)")
            {
                query = query.Where(r => r.StatusCode >= 200 && r.StatusCode < 300);
            }
        }

        // Фильтр по направлению
        if (DirectionFilterComboBox?.SelectedItem is ComboBoxItem directionItem && directionItem.Content.ToString() != "Все")
        {
            bool isIncoming = directionItem.Content.ToString() == "Входящие";
            query = query.Where(r => r.IsIncoming == isIncoming);
        }

        return query.OrderByDescending(r => r.Timestamp);
    }

    private void UpdateStatisticsLabels()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                TotalRequestsLabel.Content = _statisticsService.GetTotalRequests().ToString();
                GetRequestsLabel.Content = _statisticsService.GetRequestsByMethod("GET").ToString();
                PostRequestsLabel.Content = _statisticsService.GetRequestsByMethod("POST").ToString();
                AverageTimeLabel.Content = $"{_statisticsService.GetAverageProcessingTime():F2} мс";
                
                // Обновляем графики только если они были инициализированы
                if (_isChartsInitialized && _chartViewModel != null)
                {
                    _chartViewModel.UpdateCharts();
                }
            });
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при обновлении статистики: {ex.Message}");
        }
    }

    private async void StartServerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(ServerPortTextBox.Text, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Пожалуйста, введите корректный порт (1-65535)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Предупреждение если порт меньше 1024
            if (port < 1024)
            {
                var result = MessageBox.Show(
                    "Порты ниже 1024 требуют прав администратора. Продолжить?", 
                    "Предупреждение", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            
            StartServerButton.IsEnabled = false;
            ServerPortTextBox.IsEnabled = false;
            
            await _serverService.StartAsync(port);
            
            StopServerButton.IsEnabled = true;
            AddToServerLogs($"Сервер запущен на порту {port}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при запуске сервера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            StartServerButton.IsEnabled = true;
            ServerPortTextBox.IsEnabled = true;
        }
    }

    private void StopServerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _serverService.Stop();
            
            StopServerButton.IsEnabled = false;
            StartServerButton.IsEnabled = true;
            ServerPortTextBox.IsEnabled = true;
            
            AddToServerLogs("Сервер остановлен");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при остановке сервера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"http_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                _serverService.SaveLogsToFile(saveFileDialog.FileName);
                MessageBox.Show($"Логи сохранены в файл: {saveFileDialog.FileName}", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при сохранении логов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshStatsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatisticsLabels();
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при обновлении статистики: {ex.Message}");
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            UpdateRequestsList();
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при применении фильтров: {ex.Message}");
        }
    }

    private void RequestsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (RequestsDataGrid.SelectedItem is RequestModel request)
            {
                SelectedRequestHeadersTextBox.Text = request.Headers;
                SelectedRequestBodyTextBox.Text = request.Body;
                SelectedRequestResponseTextBox.Text = request.Response;
            }
            else
            {
                SelectedRequestHeadersTextBox.Clear();
                SelectedRequestBodyTextBox.Clear();
                SelectedRequestResponseTextBox.Clear();
            }
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при отображении деталей запроса: {ex.Message}");
        }
    }

    private void ClientMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Добавляем проверку на null для ClientRequestBodyTextBox
        if (ClientRequestBodyTextBox == null) return; 
        
        try
        {
            if (ClientMethodComboBox?.SelectedItem is ComboBoxItem item)
            {
                bool isPostMethod = item.Content.ToString() == "POST";
                ClientRequestBodyTextBox.IsEnabled = isPostMethod;
                
                if (isPostMethod && string.IsNullOrEmpty(ClientRequestBodyTextBox.Text))
                {
                    ClientRequestBodyTextBox.Text = "{\n  \"message\": \"Тестовое сообщение\"\n}";
                }
            }
        }
        catch (Exception ex)
        {
            LogDebugMessage($"Ошибка при изменении метода запроса: {ex.Message}");
        }
    }

    private async void SendRequestButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = ClientUrlTextBox.Text;
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Пожалуйста, введите URL", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string method = (ClientMethodComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string body = ClientRequestBodyTextBox.Text;
            
            SendRequestButton.IsEnabled = false;
            ClientResponseTextBox.Clear();
            ClientResponseTextBox.Text = "Отправка запроса...";
            
            var response = await _clientService.SendRequestAsync(url, method, body);
            
            ClientResponseTextBox.Text = response.Response;
        }
        catch (Exception ex)
        {
            ClientResponseTextBox.Text = $"Ошибка: {ex.Message}";
        }
        finally
        {
            SendRequestButton.IsEnabled = true;
        }
    }
}