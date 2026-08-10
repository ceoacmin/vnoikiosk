using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VnoiKiosk.Models
{
    public class VerifyRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("access_key")]
        public string AccessKey { get; set; } = string.Empty;

        [JsonProperty("machine_id")]
        public string MachineId { get; set; } = string.Empty;

        [JsonProperty("ip_address")]
        public string IpAddress { get; set; } = string.Empty;
    }

    public class VerifyResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; } = string.Empty;

        [JsonProperty("exams")]
        public List<ExamData>? Exams { get; set; }
    }

    public class ExamData
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("contest_link")]
        public string ContestLink { get; set; } = string.Empty;

        [JsonProperty("access_code")]
        public string AccessCode { get; set; } = string.Empty;
    }

    public class LocalAuthData
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
    }

    public class DeviceSelectMessage
    {
        public string action { get; set; } = string.Empty;
        public string examIndex { get; set; } = string.Empty;
    }
}