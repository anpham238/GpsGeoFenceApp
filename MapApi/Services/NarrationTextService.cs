using MapApi.Data;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public class NarrationTextService
{
    private static readonly Dictionary<string, string> NearPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi-VN"]   = "Bạn sắp đến {0}.",
        ["en-US"]   = "You are approaching {0}.",
        ["zh-Hans"] = "您即将到达{0}。",
        ["ja-JP"]   = "{0}に近づいています。",
        ["ko-KR"]   = "{0}에 가까워지고 있습니다.",
        ["de-DE"]   = "Sie nähern sich {0}.",
    };

    private static readonly Dictionary<string, string> EnterPrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi-VN"]   = "Bạn đã đến {0}.",
        ["en-US"]   = "You have arrived at {0}.",
        ["zh-Hans"] = "您已到达{0}。",
        ["ja-JP"]   = "{0}に到着しました。",
        ["ko-KR"]   = "{0}에 도착하셨습니다.",
        ["de-DE"]   = "Sie sind in {0} angekommen.",
    };

    // eventType: 0=Enter, 1=Near, 2=Tap
    public string Build(string poiName, string? tts, string lang, byte eventType)
    {
        var prefixDict = eventType == 1 ? NearPrefix : EnterPrefix;
        var template = prefixDict.TryGetValue(lang, out var t) ? t : prefixDict["vi-VN"];
        var prefix = string.Format(template, poiName);
        return string.IsNullOrWhiteSpace(tts) ? prefix : $"{prefix} {tts}";
    }

    public static byte ParseEventType(string? s) =>
        (s ?? "").Trim().ToLowerInvariant() switch
        {
            "enter" => 0,
            "near"  => 1,
            "tap"   => 2,
            _       => 0
        };
}
