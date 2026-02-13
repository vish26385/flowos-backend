using FlowOS.Api.Configurations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FlowOS.Api.Services.Notifications
{
    public class ExpoPushClient
    {
        private readonly HttpClient _http;
        private readonly ExpoPushOptions _opt;

        public ExpoPushClient(HttpClient http, ExpoPushOptions opt)
        {
            _http = http;
            _opt = opt;
        }

        public async Task<(bool ok, string? error)> SendAsync(IEnumerable<ExpoPushMessage> messages, CancellationToken ct)
        {
            var list = messages.ToList();
            if (list.Count == 0) return (true, null);

            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.SendUrl);

            if (!string.IsNullOrWhiteSpace(_opt.AccessToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.AccessToken);
            }

            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
                return (false, $"Expo HTTP {(int)res.StatusCode}: {body}");

            // Expo may return per-message errors even with 200, but we keep it simple:
            // If you want, we can parse tickets later.
            return (true, null);
        }
    }

    public class ExpoPushMessage
    {
        public string To { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public Dictionary<string, object>? Data { get; set; }
        public string? Sound { get; set; } = "default";
        public int? Ttl { get; set; } = 3600;
        public string? Priority { get; set; } = "high";
    }
}
