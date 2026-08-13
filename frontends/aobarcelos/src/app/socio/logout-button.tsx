"use client";

import { useRouter } from "next/navigation";

export function LogoutButton() {
  const router = useRouter();
  async function onClick() {
    await fetch("/socio/api/logout", { method: "POST" });
    router.push("/socio/login");
    router.refresh();
  }
  return (
    <button
      onClick={onClick}
      className="mt-4 w-full rounded border border-slate-300 px-3 py-1 text-sm hover:bg-slate-100"
    >
      Sair
    </button>
  );
}
