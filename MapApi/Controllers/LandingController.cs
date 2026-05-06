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
          .actions { display: flex; flex-direction: column; gap: 10px; margin-bottom: 16px; }
          .btn { padding: 14px; border: none; border-radius: 10px; font-size: 1rem; font-weight: 600; cursor: pointer; text-align: center; text-decoration: none; display: block; transition: opacity .15s; }
          .btn-play  { background: #1976D2; color: #fff; }
          .btn-play:hover  { background: #1565c0; }
          .btn-play:disabled { background: #333; color: #666; cursor: not-allowed; }
          .btn-pause { background: #f57c00; color: #fff; display: none; }
          .btn-replay { background: #0d47a1; color: #fff; display: none; }
          .btn-stop  { background: #b71c1c; color: #fff; display: none; }
          .status-bar { font-size: 0.82rem; color: #8b93a7; text-align: center; min-height: 18px; margin-bottom: 4px; }
          .quota-bar { height: 4px; background: #333; border-radius: 2px; margin: 6px 0 4px; }
          .quota-fill { height: 100%; background: #1976D2; border-radius: 2px; transition: width 0.4s; }
          .quota-info { font-size: 0.8rem; color: #8b93a7; text-align: center; }
          audio { width: 100%; margin-top: 14px; border-radius: 8px; }
          .narration-box { background: #1a1d27; border-radius: 10px; padding: 14px; margin-top: 16px; font-size: 0.85rem; line-height: 1.7; color: #c5cfe0; display: none; }
          .tts-bar { display: none; align-items: center; gap: 10px; margin-top: 10px; background: #1a1d27; border-radius: 10px; padding: 12px 14px; }
          .tts-bar span { font-size: 0.82rem; color: #8b93a7; flex: 1; }
          .tts-dot { width: 8px; height: 8px; border-radius: 50%; background: #1976D2; animation: pulse 1s infinite; }
          @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.3} }
          .download-card { margin-top: 24px; background: #131820; border: 1px solid #1e2d1e; border-radius: 12px; padding: 18px 16px; }
          .download-card h3 { font-size: 1rem; color: #81c784; margin-bottom: 6px; }
          .download-card p { font-size: 0.82rem; color: #8b93a7; margin-bottom: 14px; line-height: 1.5; }
          .btn-dl { display: flex; align-items: center; justify-content: center; gap: 8px; padding: 13px; border-radius: 9px; font-size: 0.95rem; font-weight: 700; text-decoration: none; border: none; cursor: pointer; width: 100%; margin-bottom: 8px; }
          .btn-dl-open    { background: #0d47a1; color: #bbdefb; }
          .btn-dl-android { background: #1b5e20; color: #a5d6a7; }
          .btn-dl-ios     { background: #1a237e; color: #9fa8da; }
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
            <button class="btn btn-play"   id="btnPlay"   onclick="playNarration()"  disabled>🎧 Phát thuyết minh</button>
            <button class="btn btn-pause"  id="btnPause"  onclick="pauseNarration()">⏸ Tạm dừng</button>
            <button class="btn btn-replay" id="btnReplay" onclick="replayNarration()">🔁 Phát lại</button>
            <button class="btn btn-stop"   id="btnStop"   onclick="stopNarration()">⏹ Dừng</button>
          </div>

          <p class="status-bar" id="statusBar"></p>

          <div class="tts-bar" id="ttsBar">
            <div class="tts-dot"></div>
            <span id="ttsStatus">Đang đọc thuyết minh...</span>
          </div>

          <audio id="audioPlayer" style="display:none"></audio>

          <div>
            <div class="quota-bar"><div class="quota-fill" id="quotaFill" style="width:100%"></div></div>
            <p class="quota-info" id="quotaInfo">Đang kiểm tra...</p>
          </div>
          <div class="narration-box" id="narrationBox"></div>

          <div class="download-card" id="downloadCard">
            <h3>📲 Ứng dụng Smart Tourism</h3>
            <p>Nghe thuyết minh không giới hạn, offline, đa ngôn ngữ và nhiều tính năng hơn.</p>

            <!-- Mở app nếu đã cài (deep link) -->
            <button id="btnOpenApp" class="btn-dl btn-dl-open" onclick="openApp()" style="display:none">
              🚀 Mở ứng dụng
            </button>
            <!-- Tải app nếu chưa cài -->
            <button id="btnAndroid" class="btn-dl btn-dl-android" onclick="downloadAndroid()">
              🤖 Tải ứng dụng Android
            </button>
            <a id="btnIos" class="btn-dl btn-dl-ios" href="/api/v1/app/download?platform=ios" style="display:none">
               Tải trên App Store
            </a>
          </div>
          <!-- Build progress panel — hiển thị khi đang build APK -->
          <div id="buildPanel" style="display:none; margin-top:12px; background:#0d1520; border:1px solid #1e3a5f; border-radius:10px; padding:14px;">
            <div style="display:flex; align-items:center; gap:10px; margin-bottom:8px;">
              <div class="tts-dot" id="buildDot" style="flex-shrink:0"></div>
              <span id="buildStatusText" style="font-size:0.88rem; color:#81c3e8; flex:1">Đang build app...</span>
            </div>
            <div id="buildLogBox" style="font-size:0.72rem; color:#4a7a90; max-height:110px; overflow-y:auto; font-family:monospace; line-height:1.5; white-space:pre-wrap; word-break:break-all;"></div>
          </div>
        </div>
        <div class="toast" id="toast"></div>
        <script>
        const QUOTA = 3;
        const poiId = parseInt(location.pathname.replace(/^\/p\//, ''), 10);
        const lang  = new URLSearchParams(location.search).get('lang') || 'vi-VN';
        let dk = localStorage.getItem('_dk');
        if (!dk) {
          dk = ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g, c =>
            (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16));
          localStorage.setItem('_dk', dk);
        }
        let _ttsUtter = null;
        let _lastNarrationText = null;
        let _lastAudioUrl = null;
        let _isPaused = false;
        // Cache từ init() để phát ngay trong user-gesture (tránh mobile browser block)
        let _cachedAudioUrl = null;
        let _cachedNarrationText = null;
        let _ttsKeepAlive = null;
        let _voices = [];

        // ── Voice helpers ──────────────────────────────────────────────────────
        function _loadVoices() {
          const v = window.speechSynthesis.getVoices();
          if (v.length) _voices = v;
        }
        function _pickVoice(langTag) {
          if (!_voices.length) _voices = window.speechSynthesis.getVoices();
          const prefix = langTag.split('-')[0];
          return _voices.find(v => v.lang === langTag)
              || _voices.find(v => v.lang.startsWith(prefix))
              || null;
        }
        // Tách câu theo dấu ngắt, mỗi chunk ≤ 200 ký tự (tránh TTS engine cắt giữa chừng)
        function _splitSentences(text) {
          const parts = text.split(/(?<=[.!?。！？…])\s+/);
          const chunks = []; let buf = '';
          for (const p of parts) {
            if (!p.trim()) continue;
            if ((buf + ' ' + p).length > 200 && buf) { chunks.push(buf.trim()); buf = p; }
            else buf = buf ? buf + ' ' + p : p;
          }
          if (buf.trim()) chunks.push(buf.trim());
          return chunks.length ? chunks : [text];
        }

        function showToast(msg) {
          const t = document.getElementById('toast');
          t.textContent = msg; t.style.opacity = '1';
          setTimeout(() => t.style.opacity = '0', 2800);
        }

        function setStatus(msg) {
          document.getElementById('statusBar').textContent = msg;
        }

        function setPlayButtons(state) {
          // state: 'idle' | 'loading' | 'playing' | 'paused' | 'ended'
          const play   = document.getElementById('btnPlay');
          const pause  = document.getElementById('btnPause');
          const replay = document.getElementById('btnReplay');
          const stop   = document.getElementById('btnStop');
          play.style.display   = (state === 'idle' || state === 'ended') ? 'block' : 'none';
          pause.style.display  = state === 'playing' ? 'block' : 'none';
          replay.style.display = state === 'ended'   ? 'block' : 'none';
          stop.style.display   = (state === 'playing' || state === 'paused') ? 'block' : 'none';
          play.disabled        = state === 'loading';
          if (state === 'loading') { play.style.display = 'block'; play.textContent = '⏳ Đang tải...'; }
          else if (state === 'idle' || state === 'ended') { play.textContent = '🎧 Phát thuyết minh'; }
        }

        // Mở app nếu đã cài (deep link), không fallback download
        function openApp() {
          const deepLink = `smarttourism://poi/${poiId}?lang=${lang}`;
          window.location.href = deepLink;
          // Nếu sau 1.5s không rời trang → app chưa cài → hiện nút tải
          setTimeout(() => showToast('Vui lòng cài ứng dụng để tiếp tục'), 1500);
        }

        // Tải APK Android: nếu APK chưa có/cũ → auto build trước rồi mới tải
        let _buildPollTimer = null;
        async function downloadAndroid() {
          try {
            const st = await fetch('/api/public/app/build/status').then(r => r.json());
            if (st.apkReady && st.status !== 'building') {
              window.location.href = '/api/v1/app/download?platform=android';
              return;
            }
            showBuildPanel(st.status === 'building' ? 'App đang được build...' : 'Đang khởi động build app...');
            if (st.status !== 'building')
              await fetch('/api/public/app/build', { method: 'POST' });
            startBuildPoll();
          } catch {
            // Nếu không check được status → tải thẳng (fallback)
            window.location.href = '/api/v1/app/download?platform=android';
          }
        }
        function showBuildPanel(msg) {
          const p = document.getElementById('buildPanel');
          p.style.display = 'block';
          document.getElementById('buildStatusText').textContent = msg || 'Đang build app...';
          const dot = document.getElementById('buildDot');
          dot.style.animation = 'pulse 1s infinite';
          dot.style.background = '#1976D2';
        }
        function updateBuildPanel(d) {
          if (d.logTail && d.logTail.length) {
            const lb = document.getElementById('buildLogBox');
            lb.textContent = d.logTail.join('\n');
            lb.scrollTop = lb.scrollHeight;
          }
          const dot = document.getElementById('buildDot');
          const txt = document.getElementById('buildStatusText');
          if (d.status === 'building') {
            txt.textContent = 'Đang build app... (có thể mất 2–5 phút)';
          } else if (d.status === 'success') {
            txt.textContent = '✅ Build xong! Đang tải APK...';
            dot.style.animation = 'none'; dot.style.background = '#4caf50';
          } else if (d.status === 'failed') {
            txt.textContent = '❌ Build thất bại: ' + (d.lastError || 'Xem server log');
            dot.style.animation = 'none'; dot.style.background = '#f44336';
          }
        }
        function startBuildPoll() {
          if (_buildPollTimer) clearInterval(_buildPollTimer);
          _buildPollTimer = setInterval(async () => {
            try {
              const d = await fetch('/api/public/app/build/status').then(r => r.json());
              updateBuildPanel(d);
              if (d.status === 'success') {
                clearInterval(_buildPollTimer); _buildPollTimer = null;
                setTimeout(() => { window.location.href = '/api/v1/app/download?platform=android'; }, 1200);
              } else if (d.status === 'failed') {
                clearInterval(_buildPollTimer); _buildPollTimer = null;
                showToast('Build thất bại. Kiểm tra lại server.');
              }
            } catch { /* network hiccup, giữ poll */ }
          }, 3000);
        }

        function detectPlatform() {
          const ua = navigator.userAgent.toLowerCase();
          const isIos = /iphone|ipad|ipod/.test(ua);
          document.getElementById('btnAndroid').style.display = isIos ? 'none' : 'flex';
          document.getElementById('btnIos').style.display     = isIos ? 'flex' : 'none';
          // Luôn hiển thị "Mở app" — người dùng tự biết đã cài hay chưa
          document.getElementById('btnOpenApp').style.display = 'flex';
        }

        async function init() {
          detectPlatform();
          setPlayButtons('loading');
          setStatus('Đang tải thông tin địa điểm...');
          try {
            const res = await fetch('/api/public/poi/' + poiId + '?lang=' + lang);
            if (!res.ok) {
              document.getElementById('poiName').textContent = 'Không tìm thấy địa điểm';
              setPlayButtons('idle');
              document.getElementById('btnPlay').disabled = true;
              setStatus('');
              return;
            }
            const d = await res.json();
            document.title = (d.name || 'Thuyết minh') + ' — Smart Tourism';
            document.getElementById('poiName').textContent = d.name || '';
            document.getElementById('poiDesc').textContent = d.description || '';
            if (d.imageUrl) document.getElementById('heroImg').src = d.imageUrl;

            // Cache để playNarration() dùng ngay trong user-gesture (mobile browser)
            _cachedAudioUrl    = d.audioUrl    || null;
            _cachedNarrationText = d.narrationText || null;

            const hasContent = _cachedAudioUrl || _cachedNarrationText;
            setPlayButtons('idle');
            if (!hasContent) {
              document.getElementById('btnPlay').disabled = true;
              setStatus('POI này hiện chưa có dữ liệu thuyết minh');
            } else {
              setStatus('');
            }
          } catch {
            document.getElementById('poiName').textContent = 'Lỗi tải dữ liệu';
            setPlayButtons('idle');
            document.getElementById('btnPlay').disabled = true;
            setStatus('Không thể kết nối. Vui lòng kiểm tra mạng.');
          }
        }

        function stopNarration() {
          const audio = document.getElementById('audioPlayer');
          audio.pause(); audio.currentTime = 0; audio.style.display = 'none';
          if (_ttsUtter) { window.speechSynthesis.cancel(); _ttsUtter = null; }
          if (_ttsKeepAlive) { clearInterval(_ttsKeepAlive); _ttsKeepAlive = null; }
          document.getElementById('ttsBar').style.display = 'none';
          _isPaused = false;
          setPlayButtons('idle');
          setStatus('');
        }

        function pauseNarration() {
          const audio = document.getElementById('audioPlayer');
          if (!audio.paused) { audio.pause(); _isPaused = true; }
          else if (_ttsUtter) { window.speechSynthesis.pause(); _isPaused = true; }
          setPlayButtons('paused');
          setStatus('Đã tạm dừng');
        }

        function replayNarration() {
          stopNarration();
          if (_lastAudioUrl) playAudioUrl(_lastAudioUrl);
          else if (_lastNarrationText) speakText(_lastNarrationText);
        }

        function playAudioUrl(url) {
          _lastAudioUrl = url;
          const audio = document.getElementById('audioPlayer');
          audio.src = url; audio.style.display = 'block';
          setPlayButtons('loading');
          setStatus('Đang tải audio...');
          audio.oncanplay = () => {
            audio.play().then(() => {
              setPlayButtons('playing');
              setStatus('Đang phát...');
            }).catch(() => {
              setPlayButtons('idle');
              setStatus('');
              showToast('Nhấn phát để nghe thuyết minh');
            });
          };
          audio.onerror = () => {
            setStatus('Không thể tải file audio');
            setPlayButtons('idle');
          };
          audio.onended = () => {
            setPlayButtons('ended');
            setStatus('Đã phát xong');
          };
          audio.onpause = () => {
            if (!audio.ended) { setPlayButtons('paused'); setStatus('Đã tạm dừng'); }
          };
          audio.onplay = () => { setPlayButtons('playing'); setStatus('Đang phát...'); };
        }

        function speakText(text) {
          _lastNarrationText = text;
          const ss = window.speechSynthesis;
          if (!ss) { showToast('Trình duyệt không hỗ trợ đọc thoại'); setPlayButtons('idle'); return; }

          ss.cancel();
          if (_ttsKeepAlive) { clearInterval(_ttsKeepAlive); _ttsKeepAlive = null; }

          const sentences = _splitSentences(text);
          let idx = 0;
          _ttsUtter = {}; // placeholder để stop()/pause() không bị null-check trước khi phát

          function speakNext() {
            if (idx >= sentences.length || !_ttsUtter) {
              // Hoàn tất tất cả câu
              document.getElementById('ttsBar').style.display = 'none';
              if (_ttsKeepAlive) { clearInterval(_ttsKeepAlive); _ttsKeepAlive = null; }
              _ttsUtter = null; _isPaused = false;
              setPlayButtons('ended'); setStatus('Đã đọc xong');
              return;
            }
            const utter = new SpeechSynthesisUtterance(sentences[idx]);
            utter.lang = lang;
            utter.rate = 0.95;
            const voice = _pickVoice(lang);
            if (voice) utter.voice = voice;

            utter.onstart = () => {
              document.getElementById('ttsBar').style.display = 'flex';
              document.getElementById('ttsStatus').textContent =
                sentences.length > 1
                  ? `Đang đọc thuyết minh... (${idx + 1}/${sentences.length})`
                  : 'Đang đọc thuyết minh...';
              setPlayButtons('playing');
              setStatus('Đang đọc thuyết minh...');
            };
            utter.onend = () => { idx++; speakNext(); };
            utter.onerror = (e) => {
              // 'interrupted'/'canceled' = người dùng bấm stop — không báo lỗi
              if (e.error === 'interrupted' || e.error === 'canceled') return;
              document.getElementById('ttsBar').style.display = 'none';
              if (_ttsKeepAlive) { clearInterval(_ttsKeepAlive); _ttsKeepAlive = null; }
              _ttsUtter = null;
              setPlayButtons('idle');
              setStatus('Lỗi đọc thuyết minh' + (e.error ? ': ' + e.error : ''));
            };

            _ttsUtter = utter; // cập nhật để stop()/pause() cancel đúng utterance
            ss.speak(utter);
          }

          // Chrome Android: tự pause sau ~15s — resume mỗi 10s để giữ tiếp
          _ttsKeepAlive = setInterval(() => {
            if (ss.paused && !_isPaused) ss.resume();
          }, 10000);

          setPlayButtons('playing');
          setStatus('Đang chuẩn bị đọc...');
          speakNext();
        }

        async function playNarration() {
          if (_isPaused) {
            const audio = document.getElementById('audioPlayer');
            if (audio.src && audio.readyState > 0) { audio.play(); return; }
            if (_ttsUtter) { window.speechSynthesis.resume(); setPlayButtons('playing'); setStatus('Đang phát...'); return; }
            _isPaused = false;
          }

          // ── BƯỚC 1: Phát NGAY bằng cache trong user-gesture ──────────────────
          // Mobile browser (Chrome/Safari) chặn audio.play() và speechSynthesis.speak()
          // nếu gọi sau async/await. Phải gọi trước khi await bất kỳ Promise nào.
          let startedFromCache = false;
          if (_cachedAudioUrl) {
            playAudioUrl(_cachedAudioUrl);
            startedFromCache = true;
          } else if (_cachedNarrationText) {
            speakText(_cachedNarrationText);
            startedFromCache = true;
          } else {
            setPlayButtons('loading');
            setStatus('Đang xử lý...');
          }

          // ── BƯỚC 2: Gọi API quota song song (không chờ trước khi phát) ───────
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
              ? `Đã dùng ${d.playCount}/${QUOTA} lượt nghe trên web`
              : (d.message || 'Đã hết lượt nghe trên web.');

            if (!d.allowed) {
              // Hết quota → dừng phát đã start từ cache
              stopNarration();
              showToast('Hết lượt nghe. Vui lòng tải app!');
              setStatus('Hết lượt nghe web');
              return;
            }

            // Hiển thị text nếu có
            const narText = d.narrationText || _cachedNarrationText;
            if (narText) {
              _lastNarrationText = narText;
              const box = document.getElementById('narrationBox');
              box.textContent = narText; box.style.display = 'block';
            }

            // Nếu cache không có nhưng API trả về (trường hợp hiếm) → phát từ API
            if (!startedFromCache) {
              if (d.audioUrl) playAudioUrl(d.audioUrl);
              else if (d.narrationText) speakText(d.narrationText);
              else { setPlayButtons('idle'); setStatus('POI này hiện chưa có dữ liệu thuyết minh'); }
            }
          } catch {
            if (!startedFromCache) { setPlayButtons('idle'); setStatus(''); }
            showToast('Lỗi kết nối. Quota chưa được kiểm tra.');
          }
        }

        // Preload voices: Firefox trả về ngay, Chrome cần chờ voiceschanged
        if (window.speechSynthesis) {
          window.speechSynthesis.onvoiceschanged = _loadVoices;
          _loadVoices();
        }
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
