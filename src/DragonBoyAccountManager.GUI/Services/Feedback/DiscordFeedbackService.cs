using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DragonBoyAccountManager.GUI.Services.Feedback;

public sealed class DiscordFeedbackService : IFeedbackService
{
    public const string DefaultWebhookUrl = "https://discord.com/api/webhooks/1561067982392795256/XYLWXMyfa-aYc1T_StKK_9isrqc-Rvh61kYR20MI7X_rHBq9gymp2SkXiWBzoPKBgc9m";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "DragonBoyAccountManager-Feedback/1.0" } }
    };

    private readonly string _webhookUrl;

    public DiscordFeedbackService(string? webhookUrl = null)
    {
        _webhookUrl = string.IsNullOrWhiteSpace(webhookUrl) ? DefaultWebhookUrl : webhookUrl;
    }

    public async Task<FeedbackSendResult> SendFeedbackAsync(FeedbackReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            var (typeTitle, color) = report.Type switch
            {
                FeedbackType.BugReport => ("BÁO CÁO LỖI", 0xEF4444),
                FeedbackType.FeatureRequest => ("GÓP Ý TÍNH NĂNG", 0xF59E0B),
                FeedbackType.Optimization => ("ĐỀ XUẤT TỐI ƯU", 0x10B981),
                FeedbackType.General => ("GÓP Ý CHUNG", 0x6366F1),
                _ => ("GÓP Ý", 0xF59E0B)
            };

            var fields = new List<DiscordEmbedField>
            {
                new()
                {
                    Name = "Người gửi",
                    Value = Truncate(string.IsNullOrWhiteSpace(report.SenderName) ? "Ẩn danh" : report.SenderName.Trim(), 250),
                    Inline = true
                },
                new()
                {
                    Name = "Liên hệ",
                    Value = Truncate(string.IsNullOrWhiteSpace(report.ContactInfo) ? "Không cung cấp" : report.ContactInfo.Trim(), 250),
                    Inline = true
                },
                new()
                {
                    Name = "Phân loại",
                    Value = typeTitle,
                    Inline = true
                }
            };

            if (report.IncludeSystemInfo)
            {
                fields.Add(new DiscordEmbedField
                {
                    Name = "Phiên bản DBAM",
                    Value = Truncate(string.IsNullOrWhiteSpace(report.AppVersion) ? "Không rõ" : $"v{report.AppVersion}", 250),
                    Inline = true
                });

                fields.Add(new DiscordEmbedField
                {
                    Name = "Hệ điều hành",
                    Value = Truncate(string.IsNullOrWhiteSpace(report.OsVersion) ? "Windows" : report.OsVersion, 250),
                    Inline = true
                });

                fields.Add(new DiscordEmbedField
                {
                    Name = ".NET Runtime",
                    Value = Truncate(string.IsNullOrWhiteSpace(report.DotNetVersion) ? ".NET 10" : report.DotNetVersion, 250),
                    Inline = true
                });
            }

            var embed = new DiscordEmbed
            {
                Title = Truncate($"[{typeTitle}] {report.Title.Trim()}", 250),
                Description = Truncate(report.Content.Trim(), 4000),
                Color = color,
                Fields = fields,
                Footer = new DiscordEmbedFooter
                {
                    Text = "DragonBoy Account Manager - Feedback Bot"
                },
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var payload = new DiscordWebhookPayload
            {
                Username = "DBAM Feedback Hub",
                Embeds = [embed]
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(_webhookUrl, content, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new FeedbackSendResult(true);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new FeedbackSendResult(false, "Máy chủ Discord đang giới hạn tần suất gửi (Rate limit). Vui lòng đợi 30 giây rồi thử lại.");
            }

            var errorBody = string.Empty;
            try
            {
                errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { }

            return new FeedbackSendResult(false, $"Discord trả về mã lỗi: {(int)response.StatusCode} ({response.ReasonPhrase}). {errorBody}".Trim());
        }
        catch (HttpRequestException ex)
        {
            return new FeedbackSendResult(false, $"Lỗi kết nối mạng: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return new FeedbackSendResult(false, "Yêu cầu gửi quá thời gian (Timeout). Vui lòng thử lại sau.");
        }
        catch (Exception ex)
        {
            return new FeedbackSendResult(false, $"Lỗi hệ thống: {ex.Message}");
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..(maxLength - 3)] + "...";
    }

    private sealed class DiscordWebhookPayload
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("embeds")]
        public List<DiscordEmbed> Embeds { get; set; } = [];
    }

    private sealed class DiscordEmbed
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public int Color { get; set; }

        [JsonPropertyName("fields")]
        public List<DiscordEmbedField> Fields { get; set; } = [];

        [JsonPropertyName("footer")]
        public DiscordEmbedFooter? Footer { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }

    private sealed class DiscordEmbedField
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("inline")]
        public bool Inline { get; set; } = true;
    }

    private sealed class DiscordEmbedFooter
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }
}
