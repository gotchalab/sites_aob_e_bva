"use client";

import { useEffect, useState } from "react";
import { formatDate } from "@/lib/api";

function computeRelative(publishedAt: string): string {
  const d = new Date(publishedAt).getTime();
  if (Number.isNaN(d)) return "";
  const diffMs = Date.now() - d;
  const days = Math.floor(diffMs / (24 * 60 * 60 * 1000));
  if (days < 1) return "Hoje";
  if (days === 1) return "Ontem";
  if (days < 7) return `Há ${days} dias`;
  return formatDate(publishedAt);
}

function computeIsNew(publishedAt: string): boolean {
  const d = new Date(publishedAt).getTime();
  if (Number.isNaN(d)) return false;
  return Date.now() - d < 72 * 60 * 60 * 1000;
}

export function RelativeDate({ publishedAt }: { publishedAt: string }) {
  const [label, setLabel] = useState<string>(() => formatDate(publishedAt));
  useEffect(() => {
    setLabel(computeRelative(publishedAt));
  }, [publishedAt]);
  return <span suppressHydrationWarning>{label}</span>;
}

export function NewBadge({ publishedAt }: { publishedAt: string }) {
  const [isNew, setIsNew] = useState(false);
  useEffect(() => {
    setIsNew(computeIsNew(publishedAt));
  }, [publishedAt]);
  if (!isNew) return null;
  return (
    <div className="absolute right-3 top-3 rounded-full bg-accent-500 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-white shadow-sm">
      Novo
    </div>
  );
}
