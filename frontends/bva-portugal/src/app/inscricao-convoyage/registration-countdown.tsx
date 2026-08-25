"use client";

import { useEffect, useState } from "react";

type Props = {
  closesAtIso: string;
  onClosed?: () => void;
};

type Remaining = {
  totalMs: number;
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
};

function compute(closes: number): Remaining {
  const total = Math.max(0, closes - Date.now());
  const s = Math.floor(total / 1000);
  return {
    totalMs: total,
    days: Math.floor(s / 86400),
    hours: Math.floor((s % 86400) / 3600),
    minutes: Math.floor((s % 3600) / 60),
    seconds: s % 60,
  };
}

export function RegistrationCountdown({ closesAtIso, onClosed }: Props) {
  const closesAt = new Date(closesAtIso).getTime();
  const [remaining, setRemaining] = useState<Remaining | null>(null);

  useEffect(() => {
    const tick = () => {
      const r = compute(closesAt);
      setRemaining(r);
      if (r.totalMs <= 0) onClosed?.();
    };
    tick();
    const id = window.setInterval(tick, 1000);
    return () => window.clearInterval(id);
  }, [closesAt, onClosed]);

  const closesDate = new Date(closesAtIso);
  const closesLabel = new Intl.DateTimeFormat("pt-PT", {
    dateStyle: "long",
    timeStyle: "short",
  }).format(closesDate);

  if (remaining === null) return null;
  if (remaining.totalMs <= 0) return null;

  const urgent = remaining.totalMs <= 24 * 3600 * 1000;
  const bg = urgent
    ? "border-red-300 bg-red-50"
    : "border-brand-500/30 bg-white/70";
  const accent = urgent ? "text-red-700" : "text-brand-700";

  return (
    <div
      className={`mb-6 flex flex-col gap-3 rounded-2xl border p-4 shadow-sm md:flex-row md:items-center md:justify-between md:p-5 ${bg}`}
    >
      <div>
        <div className={`text-[11px] font-medium uppercase tracking-widest ${accent}`}>
          Inscrições encerram em
        </div>
        <div className="mt-1 text-sm text-ink-600">
          Prazo limite: <span className="font-semibold text-ink-900">{closesLabel}</span>
        </div>
      </div>
      <div className="flex items-stretch gap-2">
        <Cell value={remaining.days} label={remaining.days === 1 ? "dia" : "dias"} accent={accent} />
        <Cell value={remaining.hours} label="h" accent={accent} />
        <Cell value={remaining.minutes} label="min" accent={accent} />
        <Cell value={remaining.seconds} label="s" accent={accent} />
      </div>
    </div>
  );
}

function Cell({ value, label, accent }: { value: number; label: string; accent: string }) {
  return (
    <div className="flex min-w-[52px] flex-col items-center rounded-lg bg-white px-2 py-1.5 shadow-sm ring-1 ring-ink-900/10">
      <div className={`font-display text-xl font-bold leading-none tabular-nums md:text-2xl ${accent}`}>
        {value.toString().padStart(2, "0")}
      </div>
      <div className="mt-0.5 text-[10px] uppercase tracking-wider text-ink-500">{label}</div>
    </div>
  );
}
