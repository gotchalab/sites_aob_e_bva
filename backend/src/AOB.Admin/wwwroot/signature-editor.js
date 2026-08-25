// Wrapper para signature_pad usado nas páginas admin de edição de inscrição.
// Blazor Server chama estas funções via IJSRuntime para permitir a admin
// substituir a assinatura de um criador diretamente no backoffice.
(function () {
  const instances = new Map();

  function ensureLib(cb) {
    if (window.SignaturePad) return cb();
    const s = document.createElement("script");
    // Self-hosted em wwwroot/lib/signature_pad/ — evita CDN externo.
    s.src = "/lib/signature_pad/signature_pad.umd.min.js";
    s.onload = () => cb();
    s.onerror = () => console.error("Falha a carregar signature_pad");
    document.head.appendChild(s);
  }

  // Fonts handwriting bundled localmente em wwwroot/lib/fonts/ (via
  // fonts.css). O sistema é fallback caso o download falhe.
  // Escolha: fonts com traço fino/limpo (Kalam Light, Shadows Into Light,
  // Caveat) — Homemade Apple foi removida da rotação por ser demasiado
  // brushy/grossa; fica disponível se algum dia for pedida explicitamente.
  const FONT_STACKS = [
    { family: '"Kalam", "Segoe Script", cursive',              weight: "300" },
    { family: '"Shadows Into Light", "Segoe Script", cursive', weight: "400" },
    { family: '"Caveat", "Segoe Script", cursive',             weight: "400" },
  ];

  // Espera que uma font específica esteja carregada (via Font Loading API).
  // Devolve promise que resolve mesmo se não conseguir (não bloqueia).
  function waitFont(fontCss) {
    if (!document.fonts?.load) return Promise.resolve();
    return document.fonts.load(fontCss).catch(() => {});
  }

  // Escolhe cursivamente de um array, com peso opcional.
  function pick(arr) { return arr[Math.floor(Math.random() * arr.length)]; }
  function jitter(range) { return (Math.random() - 0.5) * range; }

  function resize(canvas, pad) {
    const ratio = Math.max(window.devicePixelRatio || 1, 1);
    const cssW = canvas.offsetWidth;
    const cssH = canvas.offsetHeight;
    const data = pad.toData();
    canvas.width = cssW * ratio;
    canvas.height = cssH * ratio;
    const ctx = canvas.getContext("2d");
    ctx.scale(ratio, ratio);
    pad.clear();
    if (data.length > 0) pad.fromData(data);
  }

  window.SignatureEditor = {
    init(canvasId) {
      ensureLib(() => {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const pad = new window.SignaturePad(canvas, {
          backgroundColor: "rgb(255,255,255)",
          penColor: "rgb(0,0,0)",
          minWidth: 0.6,
          maxWidth: 2.2,
        });
        instances.set(canvasId, { pad, canvas });
        resize(canvas, pad);
        const onResize = () => resize(canvas, pad);
        window.addEventListener("resize", onResize);
        // Guarda o listener para poder remover no destroy.
        instances.get(canvasId).onResize = onResize;
      });
    },
    clear(canvasId) {
      const it = instances.get(canvasId);
      if (it) {
        it.pad.clear();
        it.generated = false;
      }
    },
    isEmpty(canvasId) {
      const it = instances.get(canvasId);
      if (!it) return true;
      if (it.generated) return false;
      return it.pad.isEmpty();
    },

    // Gera uma "assinatura" a partir do nome usando fonts handwriting reais
    // (Caveat / Homemade Apple) bundled em wwwroot/lib/fonts/. Com uma font
    // handwriting decente o resultado já é orgânico — basta desenhar o nome
    // com leve inclinação e variação de tamanho. Sem wobble/slicing/multi-pass
    // porque a própria font tem essas irregularidades desenhadas.
    async generateFromName(canvasId, name) {
      const it = instances.get(canvasId);
      if (!it) return;
      const canvas = it.canvas;
      const ctx = canvas.getContext("2d");
      const cssW = canvas.offsetWidth;
      const cssH = canvas.offsetHeight;

      it.pad.clear();
      it.generated = true;

      const parts = (name || "Sem nome").trim().split(/\s+/).filter(Boolean);
      const displayName = parts.length <= 2 ? parts.join(" ") : `${parts[0]} ${parts[parts.length - 1]}`;

      // Escolhe uma das fonts finas para dar variedade entre gerações.
      const { family, weight } = pick(FONT_STACKS);

      // Tamanho: assinaturas reais quase preenchem verticalmente o espaço
      // disponível. Pequeno jitter para não ser sempre igual.
      let fontSize = Math.floor(cssH * (0.55 + Math.random() * 0.15));

      const setFont = (sz) => { ctx.font = `${weight} ${sz}px ${family}`; };
      setFont(fontSize);
      await waitFont(`${weight} ${fontSize}px ${family}`);
      setFont(fontSize);

      let measured = ctx.measureText(displayName).width;
      const maxW = cssW * 0.85;
      if (measured > maxW) {
        fontSize = Math.floor(fontSize * (maxW / measured));
        setFont(fontSize);
        measured = ctx.measureText(displayName).width;
      }

      const cx = cssW / 2 + jitter(cssW * 0.03);
      const cy = cssH * (0.52 + jitter(0.04));
      const tilt = jitter(0.04); // ±~2.3°

      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate(tilt);
      ctx.textBaseline = "alphabetic";
      ctx.textAlign = "center";
      ctx.fillStyle = "rgb(15,15,25)";
      ctx.fillText(displayName, 0, 0);
      ctx.restore();
    },
    // Recorta o canvas à bounding box de pixéis não-brancos e devolve
    // dataURL PNG. Devolve string vazia se estiver em branco.
    exportCropped(canvasId) {
      const it = instances.get(canvasId);
      if (!it || it.pad.isEmpty()) return "";
      const canvas = it.canvas;
      const ctx = canvas.getContext("2d");
      const w = canvas.width, h = canvas.height;
      let img;
      try { img = ctx.getImageData(0, 0, w, h); } catch { return canvas.toDataURL("image/png"); }
      const d = img.data;
      let minX = w, minY = h, maxX = -1, maxY = -1;
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          const i = (y * w + x) * 4;
          if (d[i + 3] === 0) continue;
          if (d[i] < 240 || d[i + 1] < 240 || d[i + 2] < 240) {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
          }
        }
      }
      if (maxX < 0) return "";
      const pad = Math.round(6 * (window.devicePixelRatio || 1));
      minX = Math.max(0, minX - pad); minY = Math.max(0, minY - pad);
      maxX = Math.min(w - 1, maxX + pad); maxY = Math.min(h - 1, maxY + pad);
      const cw = maxX - minX + 1, ch = maxY - minY + 1;
      const tmp = document.createElement("canvas");
      tmp.width = cw; tmp.height = ch;
      tmp.getContext("2d").drawImage(canvas, minX, minY, cw, ch, 0, 0, cw, ch);
      return tmp.toDataURL("image/png");
    },
    destroy(canvasId) {
      const it = instances.get(canvasId);
      if (!it) return;
      if (it.onResize) window.removeEventListener("resize", it.onResize);
      it.pad.off();
      instances.delete(canvasId);
    },
  };
})();
