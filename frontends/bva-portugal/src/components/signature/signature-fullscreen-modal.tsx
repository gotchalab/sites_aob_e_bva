"use client";

import { useEffect, useRef, useState } from "react";
import SignaturePadLib from "signature_pad";
import { fitVectorToCanvas, type SignatureVectorData } from "./signature-pad";

type Props = {
  open: boolean;
  initialVector?: SignatureVectorData | null;
  onCancel: () => void;
  onConfirm: (v: SignatureVectorData) => void;
};

// Screen Orientation API: só disponível quando em fullscreen. Nem todos os
// browsers implementam .lock() — iOS Safari, por exemplo, não permite.
type OrientationLockType = "any" | "landscape" | "portrait" | "natural" |
  "landscape-primary" | "landscape-secondary" | "portrait-primary" | "portrait-secondary";
type ScreenOrientationWithLock = ScreenOrientation & {
  lock?: (o: OrientationLockType) => Promise<void>;
};

// Overlay ecrã inteiro para assinar com mais espaço no telemóvel. Sugere ao
// utilizador rodar o dispositivo para landscape, e adapta-se automaticamente
// via resize/orientationchange (o pad já suporta re-render mantendo strokes).
export function SignatureFullscreenModal({ open, initialVector, onCancel, onConfirm }: Props) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const overlayRef = useRef<HTMLDivElement | null>(null);
  const padRef = useRef<SignaturePadLib | null>(null);
  const [isEmpty, setIsEmpty] = useState(true);
  const [isPortrait, setIsPortrait] = useState(false);
  const [rotationSupported, setRotationSupported] = useState(true);

  // Bloquear scroll do body + tentar fullscreen + lock landscape (Android).
  // Em iOS Safari o .lock() falha silenciosamente e a dica manual mantém-se.
  useEffect(() => {
    if (!open) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const el = overlayRef.current;
    let entered = false;

    (async () => {
      if (!el) return;
      // Requestfullscreen precisa de gesto de utilizador — o próprio clique que
      // abriu o modal serve. Falha silenciosamente em iOS Safari (não suportado).
      try {
        if (el.requestFullscreen) {
          await el.requestFullscreen();
          entered = true;
        }
      } catch {
        /* ignorado — mantemos o modal em modo "cheia janela" sem fullscreen real */
      }
      const orientation = screen.orientation as ScreenOrientationWithLock | undefined;
      if (orientation?.lock) {
        try {
          await orientation.lock("landscape");
        } catch {
          setRotationSupported(false);
        }
      } else {
        setRotationSupported(false);
      }
    })();

    return () => {
      document.body.style.overflow = prev;
      const orientation = screen.orientation as ScreenOrientationWithLock | undefined;
      try { orientation?.unlock?.(); } catch { /* noop */ }
      if (entered && document.fullscreenElement) {
        document.exitFullscreen().catch(() => { /* noop */ });
      }
    };
  }, [open]);

  // Detectar orientação para mostrar a dica de rotação.
  useEffect(() => {
    if (!open) return;
    const check = () => setIsPortrait(window.innerHeight > window.innerWidth);
    check();
    window.addEventListener("resize", check);
    window.addEventListener("orientationchange", check);
    return () => {
      window.removeEventListener("resize", check);
      window.removeEventListener("orientationchange", check);
    };
  }, [open]);

  // Iniciar o SignaturePad — só depois de o modal estar visível (canvas mount).
  useEffect(() => {
    if (!open) return;
    const canvas = canvasRef.current;
    if (!canvas) return;

    const pad = new SignaturePadLib(canvas, {
      backgroundColor: "rgb(255,255,255)",
      penColor: "rgb(0,0,0)",
      minWidth: 0.8,
      maxWidth: 2.6,
    });
    padRef.current = pad;

    const notify = () => setIsEmpty(pad.isEmpty());
    pad.addEventListener("beginStroke", notify);
    pad.addEventListener("endStroke", notify);

    const resize = () => {
      const ratio = Math.max(window.devicePixelRatio || 1, 1);
      const cssW = canvas.offsetWidth;
      const cssH = canvas.offsetHeight;
      const data = pad.toData();
      canvas.width = cssW * ratio;
      canvas.height = cssH * ratio;
      canvas.getContext("2d")?.scale(ratio, ratio);
      pad.clear();
      if (data.length > 0) pad.fromData(data);
      notify();
    };

    // Aguarda um tick para o layout estabilizar antes do resize inicial.
    const t = setTimeout(() => {
      resize();
      // Se veio uma assinatura pré-existente do pad principal, reescala pela
      // bounding box para preencher confortavelmente o canvas grande.
      if (initialVector && initialVector.data.length > 0) {
        pad.fromData(fitVectorToCanvas(initialVector.data, canvas.offsetWidth, canvas.offsetHeight));
        notify();
      }
    }, 0);

    window.addEventListener("resize", resize);
    window.addEventListener("orientationchange", resize);
    return () => {
      clearTimeout(t);
      window.removeEventListener("resize", resize);
      window.removeEventListener("orientationchange", resize);
      pad.removeEventListener("beginStroke", notify);
      pad.removeEventListener("endStroke", notify);
      pad.off();
      padRef.current = null;
    };
  }, [open, initialVector]);

  if (!open) return null;

  return (
    <div
      ref={overlayRef}
      className="fixed inset-0 z-[100] flex flex-col bg-white"
      role="dialog"
      aria-modal="true"
      aria-label="Assinar em ecrã inteiro"
    >
      {/* Barra superior */}
      <div className="flex items-center justify-between gap-2 border-b border-ink-900/10 bg-white px-3 py-2 shadow-sm">
        <button
          type="button"
          onClick={onCancel}
          className="inline-flex min-h-[40px] items-center gap-1 rounded-full px-3 py-1.5 text-sm font-medium text-ink-700 hover:bg-ink-900/5 focus:outline-none focus:ring-2 focus:ring-ink-900/20"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M18 6 6 18" /><path d="m6 6 12 12" />
          </svg>
          Cancelar
        </button>
        <div className="flex items-center gap-2">
          <button
            type="button"
            disabled={isEmpty}
            onClick={() => padRef.current?.clear()}
            className="inline-flex min-h-[40px] items-center gap-1 rounded-full px-3 py-1.5 text-sm font-medium text-ink-700 hover:bg-ink-900/5 focus:outline-none focus:ring-2 focus:ring-ink-900/20 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Limpar
          </button>
          <button
            type="button"
            disabled={isEmpty}
            onClick={() => {
              const pad = padRef.current;
              const canvas = canvasRef.current;
              if (!pad || !canvas) return;
              onConfirm({
                data: pad.toData(),
                sourceWidth: canvas.offsetWidth,
                sourceHeight: canvas.offsetHeight,
              });
            }}
            className="inline-flex min-h-[40px] items-center gap-1 rounded-full bg-brand-500 px-4 py-1.5 text-sm font-semibold text-white shadow-sm hover:bg-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-500/40 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Confirmar
          </button>
        </div>
      </div>

      {/* Dica de rotação — só quando o browser não permite rotação automática
          (iOS Safari) e o utilizador está em portrait. */}
      {isPortrait && !rotationSupported && (
        <div className="flex items-center gap-2 border-b border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="flex-shrink-0">
            <rect x="4" y="2" width="12" height="20" rx="2" />
            <path d="M20 12h-4l2-2m-2 2 2 2" />
          </svg>
          <span>Rode o telemóvel para <b>horizontal</b> para ter mais espaço para assinar.</span>
        </div>
      )}

      {/* Área do canvas — ocupa todo o espaço restante */}
      <div className="relative flex-1 bg-ink-50 p-2">
        <div className="relative h-full w-full rounded-lg border-2 border-dashed border-ink-900/20 bg-white shadow-inner">
          {isEmpty && (
            <div className="pointer-events-none absolute inset-0 flex select-none flex-col items-center justify-center gap-1 text-ink-400">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="36" height="36" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 20h9" />
                <path d="M16.5 3.5a2.121 2.121 0 1 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
              </svg>
              <span className="text-sm font-medium tracking-wide">Assine aqui</span>
            </div>
          )}
          <canvas
            ref={canvasRef}
            className="block h-full w-full rounded-lg"
            style={{ touchAction: "none" }}
          />
        </div>
      </div>
    </div>
  );
}
