using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using HttpMonitoringSystem.Services;

namespace HttpMonitoringSystem.ViewModels
{
    public class ChartViewModel : INotifyPropertyChanged
    {
        private SeriesCollection _minuteSeries;
        private SeriesCollection _hourSeries;
        private List<string> _minuteLabels;
        private List<string> _hourLabels;
        private readonly StatisticsService _statisticsService;
        private bool _isInitialized = false;

        public SeriesCollection MinuteSeries
        {
            get => _minuteSeries;
            set
            {
                _minuteSeries = value;
                OnPropertyChanged();
            }
        }

        public SeriesCollection HourSeries
        {
            get => _hourSeries;
            set
            {
                _hourSeries = value;
                OnPropertyChanged();
            }
        }

        public List<string> MinuteLabels
        {
            get => _minuteLabels;
            set
            {
                _minuteLabels = value;
                OnPropertyChanged();
            }
        }

        public List<string> HourLabels
        {
            get => _hourLabels;
            set
            {
                _hourLabels = value;
                OnPropertyChanged();
            }
        }

        public ChartViewModel(StatisticsService statisticsService)
        {
            try
            {
                _statisticsService = statisticsService;
                Initialize();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при инициализации ChartViewModel: {ex.Message}");
                // Инициализируем пустые коллекции, чтобы избежать NullReferenceException
                SafeInitialize();
            }
        }

        private void Initialize()
        {
            try
            {
                MinuteSeries = new SeriesCollection();
                HourSeries = new SeriesCollection();
                MinuteLabels = new List<string>();
                HourLabels = new List<string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при инициализации графиков: {ex.Message}");
                throw;
            }
        }

        private void SafeInitialize()
        {
            // Безопасная инициализация в случае ошибок
            MinuteSeries = new SeriesCollection();
            HourSeries = new SeriesCollection();
            MinuteLabels = new List<string>();
            HourLabels = new List<string>();
        }

        public void UpdateCharts()
        {
            try
            {
                if (!_isInitialized)
                {
                    return;
                }

                UpdateMinuteChart();
                UpdateHourChart();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении графиков: {ex.Message}");
                // Не выбрасываем исключение дальше, чтобы не прерывать работу приложения
            }
        }

        private void UpdateMinuteChart()
        {
            try
            {
                var requestsPerMinute = _statisticsService.GetRequestsPerMinute();
                var values = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var kvp in requestsPerMinute)
                {
                    values.Add(kvp.Value);
                    labels.Add(kvp.Key.ToString("HH:mm"));
                }

                MinuteSeries.Clear();
                MinuteSeries.Add(new LineSeries
                {
                    Title = "Запросы",
                    Values = values,
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 5
                });

                MinuteLabels = labels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении MinuteChart: {ex.Message}");
            }
        }

        private void UpdateHourChart()
        {
            try
            {
                var requestsPerHour = _statisticsService.GetRequestsPerHour();
                var values = new ChartValues<int>();
                var labels = new List<string>();

                foreach (var kvp in requestsPerHour)
                {
                    values.Add(kvp.Value);
                    labels.Add(kvp.Key.ToString("dd/MM HH:00"));
                }

                HourSeries.Clear();
                HourSeries.Add(new ColumnSeries
                {
                    Title = "Запросы",
                    Values = values
                });

                HourLabels = labels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при обновлении HourChart: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 