"use client";

import { useCallback, useEffect, useRef, useState } from "react";

type Point = { x: number; y: number };

export function SignaturePad({
  value,
  onChange,
  height = 180,
}: {
  value?: string;
  onChange: (dataUrl: string | undefined) => void;
  height?: number;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const drawing = useRef(false);
  const last = useRef<Point | null>(null);
  const [isEmpty, setIsEmpty] = useState(!value);

  const setupCanvas = useCallback(() => {
    const canvas = canvasRef.current;
    const wrapper = wrapperRef.current;
    if (!canvas || !wrapper) return;
    const dpr = window.devicePixelRatio || 1;
    const rect = wrapper.getBoundingClientRect();
    canvas.width = Math.floor(rect.width * dpr);
    canvas.height = Math.floor(height * dpr);
    canvas.style.width = `${rect.width}px`;
    canvas.style.height = `${height}px`;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, rect.width, height); // fundo transparente
    ctx.lineJoin = "round";
    ctx.lineCap = "round";
    ctx.strokeStyle = "#0f172a";
    ctx.lineWidth = 2.2;
  }, [height]);

  useEffect(() => {
    setupCanvas();
    const ro = new ResizeObserver(() => setupCanvas());
    if (wrapperRef.current) ro.observe(wrapperRef.current);
    return () => ro.disconnect();
  }, [setupCanvas]);

  function getPoint(e: PointerEvent | React.PointerEvent): Point {
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function onPointerDown(e: React.PointerEvent) {
    e.preventDefault();
    (e.target as HTMLElement).setPointerCapture(e.pointerId);
    drawing.current = true;
    last.current = getPoint(e);
  }

  function onPointerMove(e: React.PointerEvent) {
    if (!drawing.current) return;
    e.preventDefault();
    const canvas = canvasRef.current!;
    const ctx = canvas.getContext("2d");
    if (!ctx || !last.current) return;
    const p = getPoint(e);
    ctx.beginPath();
    ctx.moveTo(last.current.x, last.current.y);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
    last.current = p;
    if (isEmpty) setIsEmpty(false);
  }

  function onPointerUp() {
    if (!drawing.current) return;
    drawing.current = false;
    last.current = null;
    const canvas = canvasRef.current;
    if (!canvas) return;
    onChange(cropToInk(canvas) ?? canvas.toDataURL("image/png"));
  }

  function cropToInk(canvas: HTMLCanvasElement): string | null {
    const ctx = canvas.getContext("2d");
    if (!ctx) return null;
    const w = canvas.width, h = canvas.height;
    const data = ctx.getImageData(0, 0, w, h).data;
    let minX = w, minY = h, maxX = -1, maxY = -1;
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        const a = data[(y * w + x) * 4 + 3];
        if (a > 0) {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
        }
      }
    }
    if (maxX < 0) return null;
    const pad = 8;
    minX = Math.max(0, minX - pad);
    minY = Math.max(0, minY - pad);
    maxX = Math.min(w - 1, maxX + pad);
    maxY = Math.min(h - 1, maxY + pad);
    const cw = maxX - minX + 1;
    const ch = maxY - minY + 1;
    const off = document.createElement("canvas");
    off.width = cw;
    off.height = ch;
    off.getContext("2d")!.drawImage(canvas, minX, minY, cw, ch, 0, 0, cw, ch);
    return off.toDataURL("image/png");
  }

  function clear() {
    setupCanvas();
    setIsEmpty(true);
    onChange(undefined);
  }

  return (
    <div>
      <div
        ref={wrapperRef}
        className="relative overflow-hidden rounded-lg border border-sand-300 bg-white shadow-inner"
        style={{ height, touchAction: "none" }}
      >
        <canvas
          ref={canvasRef}
          className="block h-full w-full"
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerCancel={onPointerUp}
          onPointerLeave={onPointerUp}
        />
        {isEmpty && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-sm text-ink-400">
            Assine aqui com o dedo, rato ou stylus
          </div>
        )}
      </div>
      <div className="mt-2 flex justify-between text-xs">
        <span className="text-ink-500">Desenhe a sua assinatura no espaço acima</span>
        <button
          type="button"
          onClick={clear}
          className="font-medium text-ink-600 underline underline-offset-2 hover:text-ink-900"
        >
          Limpar
        </button>
      </div>
    </div>
  );
}
