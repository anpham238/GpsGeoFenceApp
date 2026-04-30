using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

[ApiController]
[Route("api/public/poi")]
public class LandingController(AppDb db) : ControllerBase
{
    private const int WebPlayQuota = 3;

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetLandingInfo(int id, [FromQuery] string lang = "vi-VN")
    {
        var poi = await db.Pois.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (poi is null) return NotFound(new { message = "POI không tồn tại." });

        var media = await db.PoiMedia.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdPoi == id);

        var langData = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == lang);

        return Ok(new
        {
            poiId = poi.Id,
            name = poi.Name,
            description = poi.Description,
            imageUrl = media?.Image,
            audioUrl = langData?.ProAudioUrl,
            narrationText = langData?.TextToSpeech
        });
    }

    [HttpPost("{id:int}/play-narration")]
    public async Task<IActionResult> PlayNarration(int id, [FromBody] PlayNarrationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceKey))
            return BadRequest(new { message = "DeviceKey là bắt buộc." });

        var poi = await db.Pois.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (poi is null) return NotFound(new { message = "POI không tồn tại." });

        var deviceKey = req.DeviceKey.Trim()[..Math.Min(req.DeviceKey.Trim().Length, 200)];

        var usage = await db.WebNarrationUsages
            .FirstOrDefaultAsync(u => u.PoiId == id && u.DeviceKey == deviceKey);

        if (usage is null)
        {
            usage = new WebNarrationUsage
            {
                PoiId = id,
                DeviceKey = deviceKey,
                PlayCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.WebNarrationUsages.Add(usage);
        }

        if (usage.PlayCount >= WebPlayQuota)
        {
            return Ok(new
            {
                allowed = false,
                playCount = usage.PlayCount,
                quota = WebPlayQuota,
                message = $"Bạn đã dùng hết {WebPlayQuota} lượt nghe trên web. Vui lòng tải app để tiếp tục trải nghiệm."
            });
        }

        usage.PlayCount += 1;
        usage.LastPlayedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var langData = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == req.Lang);

        db.AnalyticsVisits.Add(new AnalyticsVisit
        {
            SessionId = Guid.NewGuid(),
            PoiId = id,
            Action = "WebNarrationPlay",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Ok(new
        {
            allowed = true,
            playCount = usage.PlayCount,
            quota = WebPlayQuota,
            audioUrl = langData?.ProAudioUrl,
            narrationText = langData?.TextToSpeech
        });
    }

    [HttpGet("{id:int}/download-app")]
    public IActionResult DownloadApp(int id)
    {
        return Ok(new
        {
            androidUrl = "https://play.google.com/store/apps/details?id=com.smarttourism",
            iosUrl = "https://apps.apple.com/app/smart-tourism/id000000000",
            message = "Tải app để trải nghiệm thuyết minh đầy đủ!"
        });
    }
}

[ApiController]
public class PoiLandingPageController : ControllerBase
{
    private const string LandingHtml = """
        <!DOCTYPE html>
        <html lang="vi">
        <head>
        <meta charset="UTF-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>Thuyết minh du lịch</title>
        <style>
          * { box-sizing: border-box; margin: 0; padding: 0; }
          body { font-family: 'Segoe UI', sans-serif; background: #0f1117; color: #e0e0e0; min-height: 100vh; }
          .hero { position: relative; background: #1a1d27; }
          .hero img { width: 100%; height: 220px; object-fit: cover; display: block; }
          .hero-overlay { position: absolute; bottom: 0; left: 0; right: 0; height: 80px; background: linear-gradient(transparent, #0f1117); }
          .no-image { width: 100%; height: 160px; background: linear-gradient(135deg,#1a1d27,#0f1117); display: flex; align-items: center; justify-content: center; font-size: 2.5rem; }
          .content { padding: 20px 16px; max-width: 520px; margin: 0 auto; }
          h1 { font-size: 1.4rem; color: #fff; margin-bottom: 8px; }
          .desc { color: #b7c3d7; font-size: 0.9rem; line-height: 1.6; margin-bottom: 20px; }
          .actions { display: flex; flex-direction: column; gap: 12px; margin-bottom: 20px; }
          .btn { padding: 14px; border: none; border-radius: 10px; font-size: 1rem; font-weight: 600; cursor: pointer; text-align: center; text-decoration: none; display: block; transition: opacity .15s; }
          .btn-play { background: #1976D2; color: #fff; }
          .btn-play:hover { background: #1565c0; }
          .btn-play:disabled { background: #333; color: #666; cursor: not-allowed; }
          .btn-stop { background: #b71c1c; color: #fff; display: none; }
          .btn-app { background: #1a2a1a; border: 1px solid #2d5a2d; color: #81c784; }
          .quota-bar { height: 4px; background: #333; border-radius: 2px; margin: 8px 0 4px; }
          .quota-fill { height: 100%; background: #1976D2; border-radius: 2px; transition: width 0.4s; }
          .quota-info { font-size: 0.8rem; color: #8b93a7; text-align: center; }
          audio { width: 100%; margin-top: 14px; border-radius: 8px; }
          .narration-box { background: #1a1d27; border-radius: 10px; padding: 14px; margin-top: 16px; font-size: 0.85rem; line-height: 1.7; color: #c5cfe0; display: none; }
          .tts-bar { display: none; align-items: center; gap: 10px; margin-top: 14px; background: #1a1d27; border-radius: 10px; padding: 12px 14px; }
          .tts-bar span { font-size: 0.82rem; color: #8b93a7; flex: 1; }
          .tts-dot { width: 8px; height: 8px; border-radius: 50%; background: #1976D2; animation: pulse 1s infinite; }
          @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.3} }
          .download-card { margin-top: 24px; background: #131820; border: 1px solid #1e2d1e; border-radius: 12px; padding: 18px 16px; }
          .download-card h3 { font-size: 1rem; color: #81c784; margin-bottom: 6px; }
          .download-card p { font-size: 0.82rem; color: #8b93a7; margin-bottom: 14px; line-height: 1.5; }
          .btn-dl { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 13px; border-radius: 9px; font-size: 0.95rem; font-weight: 700; text-decoration: none; border: none; cursor: pointer; width: 100%; margin-bottom: 8px; }
          .btn-dl-android { background: #1b5e20; color: #a5d6a7; }
          .btn-dl-ios { background: #1a237e; color: #9fa8da; }
          .toast { position: fixed; bottom: 20px; left: 50%; transform: translateX(-50%); background: #333; color: #fff; padding: 10px 20px; border-radius: 20px; font-size: 0.85rem; opacity: 0; transition: opacity 0.3s; pointer-events: none; z-index: 99; white-space: nowrap; }
        </style>
        </head>
        <body>
        <div class="hero">
          <div class="no-image" id="noImage">📍</div>
          <img id="heroImg" style="display:none" alt="" onload="this.style.display='block';document.getElementById('noImage').style.display='none'" />
          <div class="hero-overlay"></div>
        </div>
        <div class="content">
          <h1 id="poiName">Đang tải...</h1>
          <p class="desc" id="poiDesc"></p>
          <div class="actions">
            <button class="btn btn-play" id="btnPlay" onclick="playNarration()" disabled>🎧 Phát thuyết minh</button>
            <button class="btn btn-stop" id="btnStop" onclick="stopNarration()">⏹ Dừng</button>
          </div>
          <div class="tts-bar" id="ttsBar">
            <div class="tts-dot"></div>
            <span id="ttsStatus">Đang đọc thuyết minh...</span>
          </div>
          <audio id="audioPlayer" controls style="display:none"></audio>
          <div>
            <div class="quota-bar"><div class="quota-fill" id="quotaFill" style="width:100%"></div></div>
            <p class="quota-info" id="quotaInfo">Đang kiểm tra...</p>
          </div>
          <div class="narration-box" id="narrationBox"></div>

          <div class="download-card" id="downloadCard">
            <h3>📲 Ứng dụng Smart Tourism</h3>
            <p>Nghe thuyết minh không giới hạn, offline, đa ngôn ngữ và nhiều tính năng hơn.</p>
            <button id="btnAndroid" class="btn-dl btn-dl-android" onclick="openOrDownloadAndroid()">
              🤖 Mở / Tải ứng dụng Android
            </button>
            <a id="btnIos" class="btn-dl btn-dl-ios" href="/api/v1/app/download?platform=ios" style="display:none">
               Tải trên App Store
            </a>
          </div>
        </div>
        <div class="toast" id="toast"></div>
        <script>
        const QUOTA = 3;
        const poiId = parseInt(location.pathname.replace(/^\/p\//, ''), 10);
        const lang = new URLSearchParams(location.search).get('lang') || 'vi-VN';
        let dk = localStorage.getItem('_dk');
        if (!dk) { dk = ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g,c=>(c^crypto.getRandomValues(new Uint8Array(1))[0]&15>>c/4).toString(16)); localStorage.setItem('_dk', dk); }
        let _ttsUtter = null;

        function showToast(msg) {
          const t = document.getElementById('toast');
          t.textContent = msg; t.style.opacity = '1';
          setTimeout(() => t.style.opacity = '0', 2800);
        }

        // Mở app nếu đã cài, fallback tải APK nếu chưa cài (Android Intent URL)
        function openOrDownloadAndroid() {
          const apkFallback = encodeURIComponent(location.origin + '/api/v1/app/download?platform=android');
          const intentUrl = `intent://poi/${poiId}?lang=${lang}#Intent;scheme=smarttourism;package=com.companyname.mauiapp1;S.browser_fallback_url=${apkFallback};end`;
          window.location.href = intentUrl;
        }

        // Hiển thị nút tải đúng nền tảng
        function detectPlatformDownload() {
          const ua = navigator.userAgent.toLowerCase();
          const isIos = /iphone|ipad|ipod/.test(ua);
          document.getElementById('btnAndroid').style.display = isIos ? 'none' : 'flex';
          document.getElementById('btnIos').style.display = isIos ? 'flex' : 'none';
        }

        async function init() {
          detectPlatformDownload();
          try {
            const res = await fetch('/api/public/poi/' + poiId + '?lang=' + lang);
            if (!res.ok) { document.getElementById('poiName').textContent = 'Không tìm thấy địa điểm'; return; }
            const d = await res.json();
            document.title = (d.name || 'Thuyết minh') + ' — Smart Tourism';
            document.getElementById('poiName').textContent = d.name || '';
            document.getElementById('poiDesc').textContent = d.description || '';
            if (d.imageUrl) document.getElementById('heroImg').src = d.imageUrl;
            document.getElementById('btnPlay').disabled = false;
          } catch { document.getElementById('poiName').textContent = 'Lỗi tải dữ liệu'; }
        }

        function stopNarration() {
          // Dừng audio file nếu đang phát
          const audio = document.getElementById('audioPlayer');
          audio.pause(); audio.currentTime = 0;
          // Dừng TTS nếu đang đọc
          if (_ttsUtter) { window.speechSynthesis.cancel(); _ttsUtter = null; }
          document.getElementById('ttsBar').style.display = 'none';
          document.getElementById('btnStop').style.display = 'none';
          document.getElementById('btnPlay').style.display = 'block';
        }

        function speakText(text) {
          if (!window.speechSynthesis) { showToast('Trình duyệt không hỗ trợ đọc thoại'); return; }
          window.speechSynthesis.cancel();
          _ttsUtter = new SpeechSynthesisUtterance(text);
          _ttsUtter.lang = lang;
          _ttsUtter.rate = 0.95;
          // Ưu tiên giọng tiếng Việt nếu có
          const voices = window.speechSynthesis.getVoices();
          const matchVoice = voices.find(v => v.lang.startsWith(lang.split('-')[0]));
          if (matchVoice) _ttsUtter.voice = matchVoice;
          _ttsUtter.onend = () => {
            document.getElementById('ttsBar').style.display = 'none';
            document.getElementById('btnStop').style.display = 'none';
            document.getElementById('btnPlay').style.display = 'block';
          };
          document.getElementById('ttsBar').style.display = 'flex';
          document.getElementById('btnStop').style.display = 'block';
          document.getElementById('btnPlay').style.display = 'none';
          window.speechSynthesis.speak(_ttsUtter);
        }

        async function playNarration() {
          const btn = document.getElementById('btnPlay');
          btn.disabled = true;
          try {
            const res = await fetch('/api/public/poi/' + poiId + '/play-narration', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ deviceKey: dk, lang })
            });
            const d = await res.json();
            const pct = d.allowed ? Math.max(0, (QUOTA - d.playCount) / QUOTA * 100) : 0;
            document.getElementById('quotaFill').style.width = pct + '%';
            document.getElementById('quotaInfo').textContent = d.allowed
              ? 'Đã dùng ' + d.playCount + '/' + QUOTA + ' lượt nghe trên web'
              : (d.message || 'Đã hết lượt nghe trên web.');
            if (!d.allowed) {
              showToast('Hết lượt nghe. Vui lòng tải app!');
              btn.disabled = false;
              return;
            }
            if (d.audioUrl) {
              // Có file audio thật → phát audio
              const audio = document.getElementById('audioPlayer');
              audio.src = d.audioUrl; audio.style.display = 'block';
              audio.play().catch(() => {});
              document.getElementById('btnStop').style.display = 'block';
              document.getElementById('btnPlay').style.display = 'none';
              audio.onended = () => {
                document.getElementById('btnStop').style.display = 'none';
                document.getElementById('btnPlay').style.display = 'block';
              };
            } else if (d.narrationText) {
              // Không có audio → dùng TTS trình duyệt
              speakText(d.narrationText);
            } else {
              showToast('Chưa có nội dung thuyết minh cho địa điểm này.');
            }
            if (d.narrationText) {
              const box = document.getElementById('narrationBox');
              box.textContent = d.narrationText; box.style.display = 'block';
            }
            btn.disabled = false;
          } catch { showToast('Lỗi kết nối. Vui lòng thử lại.'); btn.disabled = false; }
        }

        // Load voices sau khi trang sẵn sàng (iOS/Android cần chờ)
        if (window.speechSynthesis) window.speechSynthesis.onvoiceschanged = () => {};

        init();
        </script>
        </body>
        </html>
        """;

    [HttpGet("/p/{id:int}")]
    public ContentResult LandingPage(int id) =>
        Content(LandingHtml, "text/html", System.Text.Encoding.UTF8);
}

public sealed class PlayNarrationRequest
{
    public string DeviceKey { get; set; } = string.Empty;
    public string Lang { get; set; } = "vi-VN";
}
