// Mikrofon di build WebGL: Unity tidak menyediakan Microphone untuk browser, jadi kerasnya suara
// dibaca lewat Web Audio (getUserMedia + AnalyserNode) dan dikirim ke C# sebagai RMS 0–1.
// Status: 0 belum diminta atau menunggu izin, 1 menyala, -1 ditolak atau tidak didukung.
mergeInto(LibraryManager.library, {
  MicLevel_Start: function () {
    var m = window.alasMic;
    if (m && m.state !== -1) return;
    m = window.alasMic = { level: 0, state: 0 };
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { m.state = -1; return; }

    navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } })
      .then(function (stream) {
        var ctx = new (window.AudioContext || window.webkitAudioContext)();
        var analyser = ctx.createAnalyser();
        analyser.fftSize = 1024;
        ctx.createMediaStreamSource(stream).connect(analyser);
        ctx.resume();
        var samples = new Float32Array(analyser.fftSize);
        m.stream = stream;
        m.ctx = ctx;
        m.state = 1;
        (function tick() {
          analyser.getFloatTimeDomainData(samples);
          var sum = 0;
          for (var i = 0; i < samples.length; i++) sum += samples[i] * samples[i];
          m.level = Math.sqrt(sum / samples.length);
          m.frame = requestAnimationFrame(tick);
        })();
      })
      .catch(function () { m.state = -1; });
  },

  MicLevel_Stop: function () {
    var m = window.alasMic;
    if (!m) return;
    if (m.frame) cancelAnimationFrame(m.frame);
    if (m.stream) m.stream.getTracks().forEach(function (t) { t.stop(); });
    if (m.ctx) m.ctx.close();
    window.alasMic = null;
  },

  MicLevel_Get: function () {
    return window.alasMic ? window.alasMic.level : 0;
  },

  MicLevel_State: function () {
    return window.alasMic ? window.alasMic.state : 0;
  }
});
