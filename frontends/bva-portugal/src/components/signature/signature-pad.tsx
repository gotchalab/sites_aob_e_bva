"use client";

import { useEffect, useImperativeHandle, useRef, forwardRef, useState } from "react";
import SignaturePadLib, { type PointGroup } from "signature_pad";

export type SignatureVectorData = {
  data: PointGroup[];
  // Dimensões CSS do canvas onde os pontos foram capturados. Necessário para
  // reescalar proporcionalmente ao importar num canvas de tamanho diferente.
  sourceWidth: number;
  sourceHeight: number;
};

// Analisa os pixéis do canvas e devolve um PNG cropado à bounding box dos
// traços (pixéis não-brancos), com pequena margem. Usado ao submeter a
// assinatura para que o PDF não tenha whitespace acima/abaixo dos traços.
// Devolve null se o canvas estiver todo branco (nada assinado).
function cropCanvasToInk(canvas: HTMLCanvasElement, padPx = 6): string | null {
  const ctx = canvas.getContext("2d");
  if (!ctx) return null;
  const w = canvas.width;
  const h = canvas.height;
  if (w === 0 || h === 0) return null;
  let img: ImageData;
  try {
    img = ctx.getImageData(0, 0, w, h);
  } catch {
    return null; // canvas tainted (raro) — cai para fallback
  }
  const d = img.data;
  let minX = w, minY = h, maxX = -1, maxY = -1;
  // Consideramos "tinta" qualquer pixel opaco cuja luminância < ~240 —
  // captura preto puro e cinzas claros dos anti-aliased edges.
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4;
      const a = d[i + 3];
      if (a === 0) continue;
      const r = d[i], g = d[i + 1], b = d[i + 2];
      if (r < 240 || g < 240 || b < 240) {
        if (x < minX) minX = x;
        if (y < minY) minY = y;
        if (x > maxX) maxX = x;
        if (y > maxY) maxY = y;
      }
    }
  }
  if (maxX < 0) return null; // canvas em branco

  const pad = Math.round(padPx * (window.devicePixelRatio || 1));
  minX = Math.max(0, minX - pad);
  minY = Math.max(0, minY - pad);
  maxX = Math.min(w - 1, maxX + pad);
  maxY = Math.min(h - 1, maxY + pad);
  const cropW = maxX - minX + 1;
  const cropH = maxY - minY + 1;
  const tmp = document.createElement("canvas");
  tmp.width = cropW;
  tmp.height = cropH;
  const tctx = tmp.getContext("2d");
  if (!tctx) return null;
  tctx.drawImage(canvas, minX, minY, cropW, cropH, 0, 0, cropW, cropH);
  return tmp.toDataURL("image/png");
}

// Reescala os traços para caberem no canvas destino usando a bounding box
// dos próprios pontos (não as dimensões do canvas origem). Assim a assinatura
// preenche bem o destino mesmo quando foi feita numa pequena zona de um canvas
// grande — evita que ao trazer do fullscreen fique minúscula ao centro.
// Deixa uma margem interior configurável (default 8%) para respirar.
export function fitVectorToCanvas(
  data: PointGroup[],
  targetWidth: number,
  targetHeight: number,
  paddingPct = 0.08,
): PointGroup[] {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const g of data) {
    for (const p of g.points) {
      if (p.x < minX) minX = p.x;
      if (p.y < minY) minY = p.y;
      if (p.x > maxX) maxX = p.x;
      if (p.y > maxY) maxY = p.y;
    }
  }
  if (!isFinite(minX) || !isFinite(minY)) return data; // sem pontos, devolve tal como está

  const bboxW = Math.max(1, maxX - minX);
  const bboxH = Math.max(1, maxY - minY);
  const availW = targetWidth * (1 - 2 * paddingPct);
  const availH = targetHeight * (1 - 2 * paddingPct);
  const scale = Math.min(availW / bboxW, availH / bboxH);

  const centerX = (minX + maxX) / 2;
  const centerY = (minY + maxY) / 2;
  const targetCX = targetWidth / 2;
  const targetCY = targetHeight / 2;

  return data.map((g) => ({
    ...g,
    points: g.points.map((p) => ({
      ...p,
      x: (p.x - centerX) * scale + targetCX,
      y: (p.y - centerY) * scale + targetCY,
    })),
  }));
}

export type SignaturePadHandle = {
  isEmpty: () => boolean;
  clear: () => void;
  toDataUrl: () => string;
  // Devolve os traços em formato vetorial + dimensões do canvas actual, para
  // permitir reescalar sem perda ao importar noutro pad.
  getVectorData: () => SignatureVectorData | null;
  // Importa vetores de outro pad (ex.: do modo ecrã inteiro) reescalando
  // uniformemente para caber no canvas actual mantendo aspect ratio.
  setVectorData: (v: SignatureVectorData) => void;
};

type Props = {
  className?: string;
  error?: boolean;
  onEmptyChange?: (isEmpty: boolean) => void;
};

// Envolve a lib signature_pad num componente React. Trata resize do canvas
// mantendo o traço nítido em ecrãs HiDPI e após rotação. Mostra placeholder
// "Assine aqui" no estado vazio + botão flutuante de limpar quando tem traço.
export const SignaturePad = forwardRef<SignaturePadHandle, Props>(function SignaturePad(
  { className, error, onEmptyChange },
  ref,
) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const padRef = useRef<SignaturePadLib | null>(null);
  const [isEmpty, setIsEmpty] = useState(true);
  // Guardar a callback num ref — o parent recria a função em cada render, o
  // que causaria o useEffect a destruir e recriar o pad (apagando o traço).
  const onEmptyChangeRef = useRef(onEmptyChange);
  useEffect(() => {
    onEmptyChangeRef.current = onEmptyChange;
  }, [onEmptyChange]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const pad = new SignaturePadLib(canvas, {
      backgroundColor: "rgb(255,255,255)",
      penColor: "rgb(0,0,0)",
      minWidth: 0.6,
      maxWidth: 2.2,
    });
    padRef.current = pad;

    const notify = () => {
      const empty = pad.isEmpty();
      setIsEmpty(empty);
      onEmptyChangeRef.current?.(empty);
    };
    pad.addEventListener("beginStroke", notify);
    pad.addEventListener("endStroke", notify);

    const resize = () => {
      const ratio = Math.max(window.devicePixelRatio || 1, 1);
      const cssW = canvas.offsetWidth;
      const cssH = canvas.offsetHeight;
      const data = pad.toData();
      canvas.width = cssW * ratio;
      canvas.height = cssH * ratio;
      const ctx = canvas.getContext("2d");
      ctx?.scale(ratio, ratio);
      pad.clear();
      if (data.length > 0) pad.fromData(data);
      notify();
    };

    resize();
    window.addEventListener("resize", resize);
    window.addEventListener("orientationchange", resize);
    return () => {
      window.removeEventListener("resize", resize);
      window.removeEventListener("orientationchange", resize);
      pad.removeEventListener("beginStroke", notify);
      pad.removeEventListener("endStroke", notify);
      pad.off();
      padRef.current = null;
    };
  }, []);

  const clear = () => {
    padRef.current?.clear();
    setIsEmpty(true);
    onEmptyChangeRef.current?.(true);
  };

  useImperativeHandle(ref, () => ({
    isEmpty: () => padRef.current?.isEmpty() ?? true,
    clear,
    // Ao exportar para o backend, recortamos o canvas à bounding box dos
    // pixéis não-brancos: o PNG resultante contém apenas os traços, sem o
    // whitespace do canvas. Isto faz com que no PDF a assinatura fique
    // colada à linha e evita o "buraco" enorme entre a assinatura e o nome.
    toDataUrl: () => {
      const canvas = canvasRef.current;
      if (!canvas) return padRef.current?.toDataURL("image/png") ?? "";
      return cropCanvasToInk(canvas) ?? canvas.toDataURL("image/png");
    },
    getVectorData: () => {
      const pad = padRef.current;
      const canvas = canvasRef.current;
      if (!pad || !canvas) return null;
      return {
        data: pad.toData(),
        sourceWidth: canvas.offsetWidth,
        sourceHeight: canvas.offsetHeight,
      };
    },
    setVectorData: (v: SignatureVectorData) => {
      const pad = padRef.current;
      const canvas = canvasRef.current;
      if (!pad || !canvas) return;
      const targetW = canvas.offsetWidth;
      const targetH = canvas.offsetHeight;
      pad.fromData(fitVectorToCanvas(v.data, targetW, targetH));
      const empty = pad.isEmpty();
      setIsEmpty(empty);
      onEmptyChangeRef.current?.(empty);
    },
  }));

  return (
    <div
      className={`relative rounded-xl border-2 bg-white shadow-inner transition ${
        error
          ? "border-red-500"
          : isEmpty
            ? "border-dashed border-ink-900/20"
            : "border-solid border-ink-900/15"
      } ${className ?? ""}`}
    >
      {/* Placeholder centrado quando vazio — puramente visual, o canvas fica por cima */}
      {isEmpty && (
        <div className="pointer-events-none absolute inset-0 flex select-none flex-col items-center justify-center gap-1 text-ink-400">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 20h9" />
            <path d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
          </svg>
          <span className="text-xs font-medium tracking-wide">Assine aqui</span>
        </div>
      )}
      <canvas
        ref={canvasRef}
        className="block h-48 w-full touch-none rounded-xl sm:h-44"
        style={{ touchAction: "none" }}
      />
      {/* Botão flutuante de limpar — só aparece quando há traço, canto superior direito */}
      {!isEmpty && (
        <button
          type="button"
          onClick={clear}
          aria-label="Limpar assinatura"
          className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-white/95 text-ink-500 shadow ring-1 ring-ink-900/10 transition hover:bg-red-50 hover:text-red-600 hover:ring-red-200 focus:outline-none focus:ring-2 focus:ring-red-400"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M18 6 6 18" />
            <path d="m6 6 12 12" />
          </svg>
        </button>
      )}
    </div>
  );
});
