// Mikrofon di build WebGL: Unity tidak menyediakan Microphone untuk browser, jadi kerasnya suara
// dibaca lewat Web Audio (getUserMedia + AnalyserNode) dan dikirim ke C# sebagai RMS 0–1.
// Status: 0 belum diminta atau menunggu izin, 1 menyala, -1 ditolak atau tidak didukung.
// AudioContext dibuat saat klik pemain dan dibangunkan lagi di setiap klik/tombol: browser membiarkan
// konteks yang dibuat di luar klik dalam keadaan "suspended", dan analyser lalu hanya membaca diam.
mergeInto(LibraryManager.library, {
  MicLevel_Start: function () {
    var m = window.alasMic;
    if (m && m.state !== -1) return;
    m = window.alasMic = { level: 0, state: 0 };
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      m.state = -1;
      console.warn('[AlasMic] getUserMedia tidak tersedia (halaman harus https atau localhost).');
      return;
    }

    var AudioCtx = window.AudioContext || window.webkitAudioContext;
    m.ctx = new AudioCtx();
    m.ctx.resume();
    m.wake = function () { if (m.ctx && m.ctx.state === 'suspended') m.ctx.resume(); };
    ['pointerdown', 'keydown', 'touchstart'].forEach(function (type) {
      document.addEventListener(type, m.wake, true);
    });

    navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: false, autoGainControl: true } })
      .then(function (stream) {
        var analyser = m.ctx.createAnalyser();
        analyser.fftSize = 1024;
        m.ctx.createMediaStreamSource(stream).connect(analyser);
        m.ctx.resume();
        var samples = new Float32Array(analyser.fftSize);
        m.stream = stream;
        m.state = 1;
        console.log('[AlasMic] Mikrofon menyala:', stream.getAudioTracks()[0].label, '— AudioContext', m.ctx.state);
        (function tick() {
          analyser.getFloatTimeDomainData(samples);
          var sum = 0;
          for (var i = 0; i < samples.length; i++) sum += samples[i] * samples[i];
          m.level = Math.sqrt(sum / samples.length);
          m.frame = requestAnimationFrame(tick);
        })();
      })
      .catch(function (error) {
        m.state = -1;
        console.warn('[AlasMic] Mikrofon gagal:', error && error.name, error && error.message);
      });
  },

  MicLevel_Stop: function () {
    var m = window.alasMic;
    if (!m) return;
    if (m.frame) cancelAnimationFrame(m.frame);
    if (m.stream) m.stream.getTracks().forEach(function (t) { t.stop(); });
    if (m.wake) ['pointerdown', 'keydown', 'touchstart'].forEach(function (type) {
      document.removeEventListener(type, m.wake, true);
    });
    if (m.ctx) m.ctx.close();
    window.alasMic = null;
  },

  MicLevel_Get: function () {
    var m = window.alasMic;
    if (!m) return 0;
    if (m.wake) m.wake();
    return m.level;
  },

  MicLevel_State: function () {
    return window.alasMic ? window.alasMic.state : 0;
  }
});
