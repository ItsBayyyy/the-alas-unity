mergeInto(LibraryManager.library, {

  MicWebStart: function () {
    if (window.theAlasMic && window.theAlasMic.started) return;
    var m = window.theAlasMic = { started: true, ready: 0, denied: 0, ctx: null, analyser: null, buf: null, stream: null };

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) { m.denied = 1; return; }

    navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: false, noiseSuppression: true, autoGainControl: false }
    }).then(function (stream) {
      var AC = window.AudioContext || window.webkitAudioContext;
      m.stream = stream;
      m.ctx = new AC();
      var src = m.ctx.createMediaStreamSource(stream);
      m.analyser = m.ctx.createAnalyser();
      m.analyser.fftSize = 1024;
      m.buf = new Float32Array(m.analyser.fftSize);
      src.connect(m.analyser);
      if (m.ctx.state === 'suspended') m.ctx.resume();
      m.ready = 1;
    }).catch(function () {
      m.denied = 1;
    });
  },

  // 1 = siap, 0 = menunggu izin, -1 = ditolak atau tidak didukung
  MicWebStatus: function () {
    var m = window.theAlasMic;
    if (!m) return 0;
    if (m.denied) return -1;
    return m.ready;
  },

  MicWebGetRms: function () {
    var m = window.theAlasMic;
    if (!m || !m.ready) return 0;
    m.analyser.getFloatTimeDomainData(m.buf);
    var sum = 0;
    for (var i = 0; i < m.buf.length; i++) sum += m.buf[i] * m.buf[i];
    return Math.sqrt(sum / m.buf.length);
  },

  MicWebStop: function () {
    var m = window.theAlasMic;
    if (!m) return;
    if (m.stream) m.stream.getTracks().forEach(function (t) { t.stop(); });
    if (m.ctx) m.ctx.close();
    window.theAlasMic = null;
  }
});