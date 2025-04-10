using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpMonitoringSystem.Models;
using Newtonsoft.Json;

namespace HttpMonitoringSystem.Services
{
    public class HttpClientService
    {
        private readonly HttpClient _httpClient;
        
        public event EventHandler<RequestModel> RequestSent;
        public event EventHandler<string> LogMessage;

        public HttpClientService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<RequestModel> SendRequestAsync(string url, string method, string body = null)
        {
            var startTime = DateTime.Now;
            var requestModel = new RequestModel
            {
                Method = method,
                Url = url,
                Body = body,
                IsIncoming = false
            };

            try
            {
                LogMessage?.Invoke(this, $"Отправка {method}-запроса: {url}");
                
                HttpResponseMessage response;
                
                switch (method.ToUpper())
                {
                    case "GET":
                        response = await _httpClient.GetAsync(url);
                        break;
                    case "POST":
                        var content = new StringContent(body ?? "", Encoding.UTF8, "application/json");
                        response = await _httpClient.PostAsync(url, content);
                        break;
                    default:
                        throw new ArgumentException($"Неподдерживаемый метод HTTP: {method}");
                }

                string responseText = await response.Content.ReadAsStringAsync();
                
                requestModel.Response = responseText;
                requestModel.StatusCode = (int)response.StatusCode;
                requestModel.Headers = GetRequestHeaders(response);
                requestModel.ProcessingTime = DateTime.Now - startTime;
                
                LogMessage?.Invoke(this, $"Ответ получен: {(int)response.StatusCode}");
                RequestSent?.Invoke(this, requestModel);
                
                return requestModel;
            }
            catch (Exception ex)
            {
                requestModel.Response = ex.Message;
                requestModel.StatusCode = 0;
                requestModel.ProcessingTime = DateTime.Now - startTime;
                
                LogMessage?.Invoke(this, $"Ошибка при отправке запроса: {ex.Message}");
                RequestSent?.Invoke(this, requestModel);
                
                return requestModel;
            }
        }

        private string GetRequestHeaders(HttpResponseMessage response)
        {
            var sb = new StringBuilder();
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
            
            foreach (var header in response.Content.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
            
            return sb.ToString();
        }
    }
} 