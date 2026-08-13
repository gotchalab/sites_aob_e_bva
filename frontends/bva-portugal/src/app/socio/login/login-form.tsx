"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function LoginForm({ redirect, initialError }: { redirect?: string; initialError?: string }) {
  const [error, setError] = useState<string | undefined>(initialError);
  const [loading, setLoading] = useState(false);
  const router = useRouter();

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(undefined);
    setLoading(true);
    const fd = new FormData(e.currentTarget);
    const res = await fetch("/socio/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: fd.get("email"), password: fd.get("password") }),
    });
    setLoading(false);
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      setError(body.error ?? `Erro ${res.status}`);
      return;
    }
    router.push(redirect && redirect.startsWith("/socio") ? redirect : "/socio");
    router.refresh();
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4">
      {error && <div className="rounded bg-red-50 p-3 text-sm text-red-700">{error}</div>}
      <label className="block">
        <span className="text-sm font-medium text-ink-700">Email</span>
        <input required type="email" name="email" className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
      </label>
      <label className="block">
        <span className="text-sm font-medium text-ink-700">Password</span>
        <input required type="password" name="password" className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
      </label>
      <button
        type="submit"
        disabled={loading}
        className="rounded bg-brand-500 px-4 py-2 text-white font-medium hover:bg-brand-600 disabled:opacity-50"
      >
        {loading ? "A entrar..." : "Entrar"}
      </button>
    </form>
  );
}
