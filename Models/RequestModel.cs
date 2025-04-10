using System;
using System.Collections.Generic;

namespace HttpMonitoringSystem.Models
{
    public class RequestModel
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string Headers { get; set; }
        public string Body { get; set; }
        public string Response { get; set; }
        public int StatusCode { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool IsIncoming { get; set; }

        public RequestModel()
        {
            Id = Guid.NewGuid();
            Timestamp = DateTime.Now;
        }
    }

    public class MessageModel
    {
        public Guid Id { get; set; }
        public string Message { get; set; }

        public MessageModel()
        {
            Id = Guid.NewGuid();
        }
    }
} 