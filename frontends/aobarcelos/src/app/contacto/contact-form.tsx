"use client";

import { useEffect, useRef, useState } from "react";
import { api, formatWaitTime, formatWaitTimeShort } from "@/lib/api";

const TURNSTILE_SITEKEY = process.env.NEXT_PUBLIC_TURNSTILE_SITEKEY;

declare global {
  interface Window {
    turnstile?: {
      render: (el: string | HTMLElement, opts: { sitekey: string; callback: (token: string) => void; theme?: string }) => string;
      reset: (id?: string) => void;
    };
  }
}

export function ContactForm() {
  const [state, setState] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();
  const [token, setToken] = useState<string>();
  const widgetContainer = useRef<HTMLDivElement>(null);
  const widgetId = useRef<string | undefined>(undefined);
  // Quando o backend devolve 429, guardamos aqui o timestamp (ms) em que o
  // rate-limit expira para mostrar contagem regressiva e desabilitar o botão.
  const [rateLimitUntil, setRateLimitUntil] = useState<number | null>(null);
  const [rateLimitTick, setRateLimitTick] = useState(0);
  useEffect(() => {
    if (rateLimitUntil === null) return;
    if (Date.now() >= rateLimitUntil) {
      setRateLimitUntil(null);
      return;
    }
    const id = setInterval(() => {
      if (Date.now() >= rateLimitUntil) {
        setRateLimitUntil(null);
        return;
      }
      setRateLimitTick((t) => t + 1);
    }, 1000);
    return () => clearInterval(id);
  }, [rateLimitUntil]);
  const rateLimitSecondsLeft =
    rateLimitUntil !== null ? Math.max(0, Math.ceil((rateLimitUntil - Date.now()) / 1000)) : 0;
  void rateLimitTick;

  // Scroll até à caixa de erro. Sem isto, em ecrãs pequenos ou com teclado
  // móvel aberto a mensagem cai fora do viewport e o utilizador não a vê.
  const errorBoxRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (state === "error" && errorBoxRef.current) {
      errorBoxRef.current.scrollIntoView({ behavior: "smooth", block: "center" });
    }
  }, [state, errorMsg, rateLimitUntil]);

  useEffect(() => {
    if (!TURNSTILE_SITEKEY || !widgetContainer.current) return;
    let cancelled = false;

    const mount = () => {
      if (cancelled || !widgetContainer.current || !window.turnstile) return;
      if (widgetId.current) return;
      widgetId.current = window.turnstile.render(widgetContainer.current, {
        sitekey: TURNSTILE_SITEKEY,
        theme: "light",
        callback: (t) => setToken(t),
      });
    };

    if (window.turnstile) {
      mount();
    } else {
      const s = document.createElement("script");
      s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js";
      s.async = true;
      s.defer = true;
      s.onload = mount;
      document.head.appendChild(s);
    }

    return () => {
      cancelled = true;
    };
  }, []);

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setState("sending");
    setErrorMsg(undefined);
    const fd = new FormData(e.currentTarget);
    const res = await api.submitContact({
      name: String(fd.get("name") ?? ""),
      email: String(fd.get("email") ?? ""),
      phone: String(fd.get("phone") ?? "") || undefined,
      subject: String(fd.get("subject") ?? ""),
      message: String(fd.get("message") ?? ""),
      turnstileToken: token,
    });
    if (res.ok) {
      setState("sent");
      (e.target as HTMLFormElement).reset();
      if (window.turnstile && widgetId.current) window.turnstile.reset(widgetId.current);
      setToken(undefined);
    } else {
      setState("error");
      if (res.status === 429) {
        const seconds = res.retryAfter && res.retryAfter > 0 ? res.retryAfter : 60;
        setRateLimitUntil(Date.now() + seconds * 1000);
        setErrorMsg(
          res.error ??
            `Foram feitas demasiadas submissões desta ligação nos últimos minutos. Aguarda ${formatWaitTime(seconds)} antes de tentar de novo.`
        );
      } else if (res.status && res.status >= 500) {
        setErrorMsg(
          "Não foi possível enviar a mensagem neste momento. Tenta novamente dentro de alguns minutos."
        );
      } else {
        setErrorMsg(res.error ?? "Erro desconhecido");
      }
    }
  }

  if (state === "sent") {
    return (
      <div className="flex items-start gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-5 text-emerald-900">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="mt-0.5 h-5 w-5 flex-shrink-0 text-emerald-600">
          <path d="M20 6 9 17l-5-5" />
        </svg>
        <div>
          <div className="font-display text-lg font-semibold">Mensagem enviada</div>
          <p className="mt-1 text-sm text-emerald-800/90">Obrigado — vamos responder em breve.</p>
        </div>
      </div>
    );
  }

  const inputCls =
    "mt-1.5 w-full rounded-lg border border-earth-800/15 bg-white px-3.5 py-2.5 text-earth-900 placeholder-earth-700/40 shadow-sm transition focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20";
  const labelCls =
    "text-[11px] font-medium uppercase tracking-widest text-earth-700/80";

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-5">
      <div className="grid gap-5 md:grid-cols-2">
        <label className="block">
          <span className={labelCls}>Nome *</span>
          <input required name="name" placeholder="O teu nome" className={inputCls} />
        </label>
        <label className="block">
          <span className={labelCls}>Email *</span>
          <input required type="email" name="email" placeholder="nome@exemplo.pt" className={inputCls} />
        </label>
        <label className="block">
          <span className={labelCls}>Telefone</span>
          <input name="phone" placeholder="Opcional" className={inputCls} />
        </label>
        <label className="block">
          <span className={labelCls}>Assunto *</span>
          <input required name="subject" placeholder="Sobre o que queres falar?" className={inputCls} />
        </label>
      </div>
      <label className="block">
        <span className={labelCls}>Mensagem *</span>
        <textarea
          required
          name="message"
          rows={7}
          placeholder="Escreve aqui a tua mensagem..."
          className={`${inputCls} resize-y`}
        />
      </label>

      {TURNSTILE_SITEKEY ? (
        <div ref={widgetContainer} />
      ) : (
        <p className="rounded-lg border border-amber-300/60 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          NEXT_PUBLIC_TURNSTILE_SITEKEY não configurado — modo dev sem verificação anti-bot.
        </p>
      )}

      {state === "error" && errorMsg && (
        <div
          ref={errorBoxRef}
          className={
            rateLimitSecondsLeft > 0
              ? "rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900"
              : "rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800"
          }
        >
          <p className="font-semibold">
            {rateLimitSecondsLeft > 0
              ? "Demasiadas tentativas — aguarda alguns minutos"
              : "Não foi possível enviar a mensagem"}
          </p>
          <p className="mt-1">{errorMsg}</p>
          {rateLimitSecondsLeft > 0 && (
            <p className="mt-2 text-xs">
              Podes tentar de novo dentro de <strong>{formatWaitTime(rateLimitSecondsLeft)}</strong>.
              Se partilhas a ligação à Internet com outras pessoas — por
              exemplo, a mesma rede de casa — o limite é comum a todos.
            </p>
          )}
        </div>
      )}

      <button
        type="submit"
        disabled={state === "sending" || rateLimitSecondsLeft > 0}
        className="mt-1 inline-flex items-center justify-center gap-2 rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {state === "sending" ? (
          "A enviar..."
        ) : rateLimitSecondsLeft > 0 ? (
          `Aguarda ${formatWaitTimeShort(rateLimitSecondsLeft)}…`
        ) : (
          <>
            Enviar mensagem
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
              <path d="M5 12h14" />
              <path d="m12 5 7 7-7 7" />
            </svg>
          </>
        )}
      </button>
    </form>
  );
}
