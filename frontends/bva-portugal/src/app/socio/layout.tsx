import Link from "next/link";
import { readSession, apiGet } from "@/lib/socio-auth";
import { LogoutButton } from "./logout-button";

type Me = {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  socioId: number | null;
};

export default async function SocioLayout({ children }: { children: React.ReactNode }) {
  const { access } = await readSession();
  const me = access ? await apiGet<Me>("/api/auth/me") : null;

  if (!me) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-10">
        {children}
      </div>
    );
  }

  return (
    <div className="mx-auto grid max-w-6xl gap-8 px-4 py-10 md:grid-cols-[220px_1fr]">
      <aside className="md:sticky md:top-8 md:h-fit">
        <div className="rounded-lg border border-sand-300 bg-white p-4 shadow-sm">
          <div className="text-sm font-semibold">{me.fullName || "Sócio"}</div>
          <div className="text-xs text-ink-500">{me.email}</div>
          <nav className="mt-4 flex flex-col gap-1 text-sm">
            <Link href="/socio" className="rounded px-2 py-1 hover:bg-sand-200">Dashboard</Link>
            <Link href="/socio/dados" className="rounded px-2 py-1 hover:bg-sand-200">Os meus dados</Link>
            <Link href="/socio/quotas" className="rounded px-2 py-1 hover:bg-sand-200">Quotas</Link>
            <Link href="/socio/anilhas" className="rounded px-2 py-1 hover:bg-sand-200">Anilhas</Link>
            <Link href="/socio/pedir-anilhas" className="rounded px-2 py-1 hover:bg-sand-200">Novo pedido</Link>
          </nav>
          <LogoutButton />
        </div>
      </aside>
      <section className="min-w-0">{children}</section>
    </div>
  );
}
