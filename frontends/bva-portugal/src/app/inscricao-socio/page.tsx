import { api } from "@/lib/api";
import { InscricaoForm } from "./inscricao-form";

export const revalidate = 3600;

export const metadata = {
  title: "Inscrição de sócio",
  description: "Preencha a proposta de admissão de sócio online.",
};

export default async function InscricaoSocioPage() {
  const site = await api.site();
  return (
    <>
      <section className="hero-pattern border-b border-sand-300">
        <div className="mx-auto max-w-5xl px-4 py-10 md:py-14">
          <div className="mb-3 inline-block rounded-full border border-brand-500/40 bg-white/60 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700">
            Torne-se sócio
          </div>
          <h1 className="font-display text-3xl font-bold leading-tight text-ink-900 md:text-5xl">
            Proposta de admissão de sócio
          </h1>
          <p className="mt-4 max-w-2xl text-base leading-relaxed text-ink-700 md:text-lg">
            Preencha o formulário abaixo. Após submissão, a sua candidatura é
            analisada em reunião de Direcção e receberá confirmação por email.
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-4xl px-4 py-8 md:py-12">
        <InscricaoForm siteName={site.name} contactEmail={site.contactEmail} />
      </div>
    </>
  );
}
