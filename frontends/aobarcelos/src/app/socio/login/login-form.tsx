"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function LoginForm({ redirect, initialError }: { redirect?: string; initialError?: string }) {
  const [error, setError] = useState<string | undefined>(initialError);
  const [loading, setLoading] = useState(false);
  const [showPass, setShowPass] = useState(false);
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
        <span className="text-sm font-medium text-slate-700">Email</span>
        <input required type="email" name="email" className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
      </label>
      <label className="block">
        <span className="text-sm font-medium text-slate-700">Password</span>
        <div className="relative mt-1">
          <input
            required
            type={showPass ? "text" : "password"}
            name="password"
            className="w-full rounded border border-slate-300 px-3 py-2 pr-10"
          />
          <button
            type="button"
            onClick={() => setShowPass((v) => !v)}
            className="absolute inset-y-0 right-2 flex items-center text-slate-400 hover:text-slate-600"
            aria-label={showPass ? "Ocultar password" : "Mostrar password"}
          >
            {showPass ? (
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
                <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
                <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
                <line x1="1" y1="1" x2="23" y2="23" />
              </svg>
            ) : (
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                <circle cx="12" cy="12" r="3" />
              </svg>
            )}
          </button>
        </div>
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
