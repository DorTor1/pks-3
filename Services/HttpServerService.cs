using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using HttpMonitoringSystem.Models;
using Newtonsoft.Json;

namespace HttpMonitoringSystem.Services
{
    public class HttpServerService
    {
        private HttpListener _listener;
        private int _port;
        private bool _isRunning;
        private CancellationTokenSource _cancellationTokenSource;
        private List<MessageModel> _messages;
        private List<RequestModel> _requests;

        public event EventHandler<RequestModel> RequestReceived;
        public event EventHandler<string> LogMessage;

        public int TotalRequests => _requests.Count;
        public int GetRequests => _requests.Count(r => r.Method == "GET" && r.IsIncoming);
        public int PostRequests => _requests.Count(r => r.Method == "POST" && r.IsIncoming);
        public TimeSpan AverageProcessingTime => _requests.Count > 0 
            ? TimeSpan.FromTicks((long)_requests.Average(r => r.ProcessingTime.Ticks)) 
            : TimeSpan.Zero;

        public DateTime StartTime { get; private set; }
        public bool IsRunning => _isRunning;
        public List<RequestModel> Requests => _requests;

        public HttpServerService()
        {
            _messages = new List<MessageModel>();
            _requests = new List<RequestModel>();
        }

        public async Task StartAsync(int port)
        {
            if (_isRunning)
                return;

            try
            {
                _port = port;
                _listener = new HttpListener();
                
                // Используем префикс с "localhost", который не требует повышенных прав
                string prefix = $"http://localhost:{_port}/";
                _listener.Prefixes.Add(prefix);
                
                LogMessage?.Invoke(this, $"Настройка сервера на {prefix}");

                _cancellationTokenSource = new CancellationTokenSource();

                // Проверяем, поддерживается ли HttpListener в текущей системе
                if (!HttpListener.IsSupported)
                {
                    throw new NotSupportedException("HttpListener не поддерживается в данной операционной системе.");
                }

                _listener.Start();
                _isRunning = true;
                StartTime = DateTime.Now;

                LogMessage?.Invoke(this, $"Сервер запущен на порту {_port}");

                var token = _cancellationTokenSource.Token;
                // Запускаем цикл прослушивания в фоновом потоке, но НЕ ждем его завершения здесь
                // await Task.Run(async () =>
                _ = Task.Run(async () => // Используем discard (_), чтобы показать, что мы намеренно не ждем задачу
                {
                    while (_isRunning && !token.IsCancellationRequested)
                    {
                        try
                        {
                            var context = await _listener.GetContextAsync();
                            _ = Task.Run(() => ProcessRequestAsync(context));
                        }
                        catch (Exception ex) when (!token.IsCancellationRequested)
                        {
                            // Проверяем, является ли ошибка ожидаемой ошибкой при остановке Listener
                            if (ex is HttpListenerException hle && hle.ErrorCode == 995) // 995 = ERROR_OPERATION_ABORTED
                            {
                                // Это ожидаемая ошибка при остановке сервера, можно проигнорировать или записать информационное сообщение
                                LogMessage?.Invoke(this, "Цикл прослушивания прерван из-за остановки сервера.");
                            }
                            else
                            {
                                // Логируем другие непредвиденные ошибки
                                LogMessage?.Invoke(this, $"Ошибка при получении контекста: {ex.Message}");
                            }
                        }
                        catch (Exception ex) when (token.IsCancellationRequested)
                        { 
                            // Если токен отменен, скорее всего, это штатная остановка, игнорируем
                            LogMessage?.Invoke(this, "Цикл прослушивания остановлен по запросу.");
                        }
                    }
                }, token);
            }
            catch (HttpListenerException ex)
            {
                _isRunning = false;
                LogMessage?.Invoke(this, $"Ошибка HttpListener: {ex.Message} (Код ошибки: {ex.ErrorCode})");
                
                if (ex.ErrorCode == 5) // Ошибка доступа
                {
                    LogMessage?.Invoke(this, "Отказано в доступе. Запустите приложение от имени администратора или используйте порт > 1024.");
                }
                
                throw;
            }
            catch (Exception ex)
            {
                _isRunning = false;
                LogMessage?.Invoke(this, $"Ошибка при запуске сервера: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            try
            {
                _listener.Stop();
                _listener.Close();
                LogMessage?.Invoke(this, "Сервер остановлен");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Ошибка при остановке сервера: {ex.Message}");
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var startTime = DateTime.Now;
            var request = context.Request;
            var response = context.Response;

            var requestModel = new RequestModel
            {
                Method = request.HttpMethod,
                Url = request.Url.ToString(),
                Headers = GetHeadersString(request),
                IsIncoming = true
            };

            try
            {
                if (request.HasEntityBody)
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        requestModel.Body = await reader.ReadToEndAsync();
                    }
                }

                LogMessage?.Invoke(this, $"Получен {request.HttpMethod}-запрос: {request.Url}");

                string responseText = "";
                int statusCode = 200;

                switch (request.HttpMethod)
                {
                    case "GET":
                        responseText = HandleGetRequest();
                        break;
                    case "POST":
                        responseText = HandlePostRequest(requestModel.Body);
                        break;
                    default:
                        responseText = "Метод не поддерживается";
                        statusCode = 405;
                        break;
                }

                var buffer = Encoding.UTF8.GetBytes(responseText);
                response.StatusCode = statusCode;
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";

                using (var output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }

                requestModel.Response = responseText;
                requestModel.StatusCode = statusCode;
                requestModel.ProcessingTime = DateTime.Now - startTime;

                lock (_requests)
                {
                    _requests.Add(requestModel);
                }

                RequestReceived?.Invoke(this, requestModel);
                LogMessage?.Invoke(this, $"Ответ отправлен: {statusCode}");
            }
            catch (Exception ex)
            {
                requestModel.Response = ex.Message;
                requestModel.StatusCode = 500;
                requestModel.ProcessingTime = DateTime.Now - startTime;

                try
                {
                    response.StatusCode = 500;
                    var buffer = Encoding.UTF8.GetBytes($"{{\"error\": \"{ex.Message}\"}}");
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "application/json";

                    using (var output = response.OutputStream)
                    {
                        await output.WriteAsync(buffer, 0, buffer.Length);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при отправке ответа об ошибке
                }

                lock (_requests)
                {
                    _requests.Add(requestModel);
                }

                RequestReceived?.Invoke(this, requestModel);
                LogMessage?.Invoke(this, $"Ошибка при обработке запроса: {ex.Message}");
            }
        }

        private string HandleGetRequest()
        {
            var serverInfo = new
            {
                StartTime = StartTime,
                Uptime = DateTime.Now - StartTime,
                TotalRequests = TotalRequests,
                GetRequests = GetRequests,
                PostRequests = PostRequests,
                AverageProcessingTimeMs = AverageProcessingTime.TotalMilliseconds
            };

            return JsonConvert.SerializeObject(serverInfo);
        }

        private string HandlePostRequest(string body)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<MessageModel>(body);
                if (message == null || string.IsNullOrEmpty(message.Message))
                {
                    throw new Exception("Неверный формат JSON или отсутствует поле 'message'");
                }

                message.Id = Guid.NewGuid();
                lock (_messages)
                {
                    _messages.Add(message);
                }

                return JsonConvert.SerializeObject(new { id = message.Id });
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обработке JSON: {ex.Message}");
            }
        }

        private string GetHeadersString(HttpListenerRequest request)
        {
            var sb = new StringBuilder();
            foreach (string key in request.Headers.AllKeys)
            {
                sb.AppendLine($"{key}: {request.Headers[key]}");
            }
            return sb.ToString();
        }

        public void SaveLogsToFile(string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, append: true))
                {
                    foreach (var request in _requests)
                    {
                        writer.WriteLine($"--- {request.Timestamp} ---");
                        writer.WriteLine($"ID: {request.Id}");
                        writer.WriteLine($"Направление: {(request.IsIncoming ? "Входящий" : "Исходящий")}");
                        writer.WriteLine($"Метод: {request.Method}");
                        writer.WriteLine($"URL: {request.Url}");
                        writer.WriteLine($"Заголовки:{Environment.NewLine}{request.Headers}");
                        
                        if (!string.IsNullOrEmpty(request.Body))
                            writer.WriteLine($"Тело запроса:{Environment.NewLine}{request.Body}");
                        
                        writer.WriteLine($"Статус ответа: {request.StatusCode}");
                        writer.WriteLine($"Ответ:{Environment.NewLine}{request.Response}");
                        writer.WriteLine($"Время обработки: {request.ProcessingTime.TotalMilliseconds} мс");
                        writer.WriteLine(new string('-', 50));
                    }
                }
                LogMessage?.Invoke(this, $"Логи сохранены в файл: {filePath}");
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"Ошибка при сохранении логов: {ex.Message}");
            }
        }
    }
} 