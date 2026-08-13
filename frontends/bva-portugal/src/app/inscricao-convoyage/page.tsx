import { api } from "@/lib/api";
import { InscricaoConvoyageForm } from "./inscricao-convoyage-form";

export const revalidate = 60;

export const metadata = {
  title: "Inscrição na Convoyage",
  description: "Inscreva as suas aves na convoyage BVA Masters online.",
};

export default async function InscricaoConvoyagePage() {
  const [site, activeYear] = await Promise.all([api.site(), api.convoyageActiveYear()]);

  return (
    <>
      <section className="hero-pattern border-b border-sand-300">
        <div className="mx-auto max-w-5xl px-4 py-10 md:py-14">
          <div className="mb-3 inline-block rounded-full border border-brand-500/40 bg-white/60 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700">
            BVA Masters{activeYear ? ` ${activeYear.year}` : ""}
          </div>
          <h1 className="font-display text-3xl font-bold leading-tight text-ink-900 md:text-5xl">
            Ficha de Inscrição — Convoyage
          </h1>
          <p className="mt-4 max-w-2xl text-base leading-relaxed text-ink-700 md:text-lg">
            {activeYear
              ? "Preencha o formulário abaixo para inscrever as suas aves na convoyage BVA Masters. Receberá uma confirmação por email com a ficha em PDF."
              : "As inscrições estão encerradas de momento. Aguarde a abertura da próxima edição."}
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-4xl px-4 py-8 md:py-12">
        {activeYear ? (
          <InscricaoConvoyageForm
            siteName={site.name}
            contactEmail={site.contactEmail}
            activeYear={activeYear}
          />
        ) : (
          <div className="rounded-2xl border border-sand-300 bg-white p-8 text-center shadow-sm">
            <p className="text-ink-600">
              As inscrições para a convoyage estão encerradas de momento.
              <br />
              Fique atento para a próxima edição.
            </p>
          </div>
        )}
      </div>
    </>
  );
}
