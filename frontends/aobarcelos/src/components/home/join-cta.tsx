import Link from "next/link";
import { ArrowRight } from "lucide-react";
import type { HomeBenefitItem, HomeConfig } from "@/lib/api-types";
import { AreaIcon } from "./icon-map";

const DEFAULT_BENEFITS: HomeBenefitItem[] = [
  {
    icon: "Award",
    label: "Anilhas federadas",
    description: "Código único de criador reconhecido pela FONP — identificação permanente e rastreável para cada ave que crias.",
  },
  {
    icon: "Calendar",
    label: "Exposições e concursos",
    description: "Participa em mostras locais e nacionais com julgamento oficial e classificação reconhecida.",
  },
  {
    icon: "ShieldCheck",
    label: "Área reservada online",
    description: "Gere quotas, pedidos de anilhas e todos os teus dados de sócio num portal exclusivo, a qualquer hora.",
  },
  {
    icon: "Users",
    label: "Comunidade e apoio técnico",
    description: "Aprende com criadores experientes, partilha conhecimento e recebe apoio técnico quando mais precisas.",
  },
];

export function JoinCta({ home }: { home: HomeConfig }) {
  const benefits = home.benefits && home.benefits.length > 0 ? home.benefits : DEFAULT_BENEFITS;
  const title = home.ctaTitle ?? "Junta-te à associação";
  const subtitle =
    home.ctaSubtitle ??
    "Anilhas federadas, exposições reconhecidas, apoio técnico e um portal online — tudo numa só associação com décadas de tradição.";
  const label = home.ctaLabel ?? "Quero ser sócio";
  const href = home.ctaHref ?? "/inscricao-socio";

  return (
    <section
      className="relative overflow-hidden text-cream-50"
      style={{
        background:
          "linear-gradient(135deg, #A05235 0%, #C56D48 55%, #A05235 100%)",
      }}
    >
      {/* Mesh gradient base */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(55% 55% at 12% 15%, rgba(249,221,164,0.28) 0%, transparent 65%)," +
            "radial-gradient(50% 55% at 88% 90%, rgba(229,199,122,0.22) 0%, transparent 65%)," +
            "radial-gradient(90% 70% at 50% 50%, rgba(47,31,31,0.35) 0%, transparent 75%)",
        }}
      />

      {/* Grelha subtil */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 opacity-[0.05]"
        style={{
          backgroundImage:
            "linear-gradient(rgba(251,247,234,0.6) 1px, transparent 1px), linear-gradient(90deg, rgba(251,247,234,0.6) 1px, transparent 1px)",
          backgroundSize: "56px 56px",
          maskImage:
            "radial-gradient(ellipse at center, black 30%, transparent 80%)",
        }}
      />

      {/* Linhas superior/inferior */}
      <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-gold-400/60 to-transparent" />
      <div className="absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-gold-400/40 to-transparent" />

      <div className="relative mx-auto grid max-w-6xl gap-14 px-4 py-24 md:py-28 lg:grid-cols-[1.05fr_1fr] lg:items-center lg:gap-16">
        {/* Coluna de conteúdo */}
        <div className="text-center lg:text-left">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-cream-50/25 bg-cream-50/10 px-3.5 py-1.5 text-[11px] font-semibold uppercase tracking-[0.14em] text-cream-50 backdrop-blur-sm">
            <span className="relative flex h-1.5 w-1.5">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-gold-400/80" />
              <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-gold-400" />
            </span>
            Torna-te sócio
          </div>

          <h2 className="font-display text-4xl font-bold leading-[1.05] tracking-tight text-white md:text-5xl lg:text-[3.5rem]">
            {title}
          </h2>

          {subtitle && (
            <p className="mx-auto mt-6 max-w-xl text-base leading-relaxed text-cream-50/90 md:text-lg lg:mx-0">
              {subtitle}
            </p>
          )}

          <div className="mt-10 flex flex-col items-center gap-3 sm:flex-row sm:gap-4 lg:items-start lg:justify-start">
            <Link
              href={href}
              className="group relative inline-flex w-full items-center justify-center gap-2 overflow-hidden rounded-full bg-cream-50 px-7 py-3.5 text-sm font-semibold text-brand-700 shadow-[0_14px_45px_-12px_rgba(47,31,31,0.55)] ring-1 ring-inset ring-earth-900/5 transition-all duration-300 hover:bg-white hover:text-brand-800 hover:shadow-[0_22px_60px_-15px_rgba(47,31,31,0.75)] sm:w-auto"
            >
              <span
                aria-hidden
                className="absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-brand-500/15 to-transparent transition-transform duration-700 group-hover:translate-x-full"
              />
              <span className="relative">{label}</span>
              <ArrowRight
                className="relative h-4 w-4 transition-transform duration-300 group-hover:translate-x-1"
                strokeWidth={2.5}
                aria-hidden
              />
            </Link>

            <Link
              href="/artigos"
              className="group inline-flex w-full items-center justify-center gap-1.5 rounded-full border border-cream-50/30 bg-cream-50/5 px-6 py-3.5 text-sm font-medium text-cream-50 backdrop-blur-sm transition-all duration-300 hover:border-gold-400/70 hover:bg-cream-50/10 hover:text-gold-400 sm:w-auto"
            >
              Saber mais sobre o clube
              <ArrowRight
                className="h-3.5 w-3.5 opacity-0 -translate-x-1 transition-all duration-300 group-hover:opacity-100 group-hover:translate-x-0"
                strokeWidth={2.5}
                aria-hidden
              />
            </Link>
          </div>

          {/* Nota de confiança */}
          <p className="mt-8 text-xs text-cream-50/60 lg:text-left">
            Processo simples · Aprovação rápida · Sem compromisso
          </p>
        </div>

        {/* Grelha de benefícios */}
        <div className="relative">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {benefits.map(({ icon, label, description }, index) => (
              <div
                key={label}
                className={
                  "group relative overflow-hidden rounded-2xl border border-cream-50/15 bg-cream-50/[0.06] p-5 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-gold-400/60 hover:bg-cream-50/[0.11] " +
                  (index % 2 === 1 ? "sm:mt-6" : "")
                }
              >
                {/* Glow no hover */}
                <div
                  aria-hidden
                  className="pointer-events-none absolute -inset-px rounded-2xl opacity-0 transition-opacity duration-300 group-hover:opacity-100"
                  style={{
                    background:
                      "radial-gradient(120px 80px at 30% 0%, rgba(249,221,164,0.22), transparent 70%)",
                  }}
                />

                <div className="relative">
                  <div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-gold-400/30 to-gold-400/5 ring-1 ring-gold-400/40 transition-transform duration-300 group-hover:scale-105">
                    <AreaIcon name={icon} className="h-5 w-5 text-gold-400" />
                  </div>
                  <h3 className="text-[15px] font-semibold text-white">
                    {label}
                  </h3>
                  <p className="mt-1 text-[13px] leading-snug text-cream-50/75">
                    {description}
                  </p>
                </div>
              </div>
            ))}
          </div>

          {/* Halo decorativo por trás dos cards */}
          <div
            aria-hidden
            className="pointer-events-none absolute -inset-8 -z-10 rounded-[2rem] bg-gradient-to-br from-gold-400/15 via-transparent to-cream-50/5 blur-2xl"
          />
        </div>
      </div>
    </section>
  );
}
