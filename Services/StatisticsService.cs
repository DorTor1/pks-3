using System;
using System.Collections.Generic;
using System.Linq;
using HttpMonitoringSystem.Models;

namespace HttpMonitoringSystem.Services
{
    public class StatisticsService
    {
        private readonly HttpServerService _serverService;
        private readonly List<RequestModel> _allRequests;

        public StatisticsService(HttpServerService serverService)
        {
            _serverService = serverService;
            _allRequests = new List<RequestModel>();
            
            _serverService.RequestReceived += (sender, request) => AddRequest(request);
        }

        public void AddRequest(RequestModel request)
        {
            lock (_allRequests)
            {
                _allRequests.Add(request);
            }
        }

        public Dictionary<DateTime, int> GetRequestsPerMinute(int minutesToShow = 30)
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddMinutes(-minutesToShow);
            
            var result = new Dictionary<DateTime, int>();
            
            // Инициализируем временные интервалы
            for (int i = 0; i < minutesToShow; i++)
            {
                var minute = startTime.AddMinutes(i);
                result[minute] = 0;
            }
            
            lock (_allRequests)
            {
                var filteredRequests = _allRequests
                    .Where(r => r.Timestamp >= startTime && r.Timestamp <= endTime)
                    .ToList();
                
                foreach (var request in filteredRequests)
                {
                    var minute = new DateTime(request.Timestamp.Year, request.Timestamp.Month, 
                        request.Timestamp.Day, request.Timestamp.Hour, request.Timestamp.Minute, 0);
                    
                    if (result.ContainsKey(minute))
                    {
                        result[minute]++;
                    }
                    else
                    {
                        result[minute] = 1;
                    }
                }
            }
            
            return result;
        }

        public Dictionary<DateTime, int> GetRequestsPerHour(int hoursToShow = 24)
        {
            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-hoursToShow);
            
            var result = new Dictionary<DateTime, int>();
            
            // Инициализируем временные интервалы
            for (int i = 0; i < hoursToShow; i++)
            {
                var hour = startTime.AddHours(i);
                result[hour] = 0;
            }
            
            lock (_allRequests)
            {
                var filteredRequests = _allRequests
                    .Where(r => r.Timestamp >= startTime && r.Timestamp <= endTime)
                    .ToList();
                
                foreach (var request in filteredRequests)
                {
                    var hour = new DateTime(request.Timestamp.Year, request.Timestamp.Month, 
                        request.Timestamp.Day, request.Timestamp.Hour, 0, 0);
                    
                    if (result.ContainsKey(hour))
                    {
                        result[hour]++;
                    }
                    else
                    {
                        result[hour] = 1;
                    }
                }
            }
            
            return result;
        }

        public List<RequestModel> GetFilteredRequests(string method = null, int? statusCode = null, 
            bool? isIncoming = null, DateTime? startTime = null, DateTime? endTime = null)
        {
            lock (_allRequests)
            {
                var query = _allRequests.AsEnumerable();
                
                if (!string.IsNullOrEmpty(method))
                {
                    query = query.Where(r => r.Method.Equals(method, StringComparison.OrdinalIgnoreCase));
                }
                
                if (statusCode.HasValue)
                {
                    query = query.Where(r => r.StatusCode == statusCode.Value);
                }
                
                if (isIncoming.HasValue)
                {
                    query = query.Where(r => r.IsIncoming == isIncoming.Value);
                }
                
                if (startTime.HasValue)
                {
                    query = query.Where(r => r.Timestamp >= startTime.Value);
                }
                
                if (endTime.HasValue)
                {
                    query = query.Where(r => r.Timestamp <= endTime.Value);
                }
                
                return query.ToList();
            }
        }

        public int GetTotalRequests() => _allRequests.Count;
        
        public int GetIncomingRequests() => _allRequests.Count(r => r.IsIncoming);
        
        public int GetOutgoingRequests() => _allRequests.Count(r => !r.IsIncoming);
        
        public int GetRequestsByMethod(string method) => 
            _allRequests.Count(r => r.Method.Equals(method, StringComparison.OrdinalIgnoreCase));
        
        public double GetAverageProcessingTime() => 
            _allRequests.Count > 0 ? _allRequests.Average(r => r.ProcessingTime.TotalMilliseconds) : 0;

        // Новый метод для получения всех запросов
        public List<RequestModel> GetAllRequests()
        {
            lock (_allRequests)
            {
                // Возвращаем копию списка, чтобы избежать изменений извне
                return new List<RequestModel>(_allRequests);
            }
        }
    }
} 