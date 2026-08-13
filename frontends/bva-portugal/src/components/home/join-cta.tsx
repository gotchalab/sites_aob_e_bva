import Link from "next/link";
import { ArrowRight } from "lucide-react";
import type { HomeBenefitItem, HomeConfig } from "@/lib/api-types";
import { AreaIcon } from "./icon-map";

const DEFAULT_BENEFITS: HomeBenefitItem[] = [
  {
    icon: "Award",
    label: "Standards oficiais",
    description: "Aceder aos padrões técnicos reconhecidos internacionalmente.",
  },
  {
    icon: "Calendar",
    label: "Acesso a exposições",
    description: "Participar nas edições BVA e nos concursos parceiros.",
  },
  {
    icon: "ShieldCheck",
    label: "Área reservada online",
    description: "Gerir quotas, dados e pedidos de anilhas num só sítio.",
  },
  {
    icon: "Users",
    label: "Comunidade técnica",
    description: "Ligação directa a criadores e juízes de Agapornis.",
  },
  {
    icon: "Plane",
    label: "Convoyage à BVA Masters",
    description: "Leva as tuas aves à maior exposição temática de Agapornis da Europa, com a delegação portuguesa.",
  },
];

export function JoinCta({ home }: { home: HomeConfig }) {
  const benefits = home.benefits && home.benefits.length > 0 ? home.benefits : DEFAULT_BENEFITS;
  const title = home.ctaTitle ?? "Junta-te à associação";
  const subtitle =
    home.ctaSubtitle ??
    "Faz parte da associação técnica portuguesa de Agapornis e liga-te à comunidade nacional de criadores.";
  const label = home.ctaLabel ?? "Quero ser sócio";
  const href = home.ctaHref ?? "/inscricao-socio";

  return (
    <section
      className="relative overflow-hidden text-sand-50"
      style={{
        background:
          "linear-gradient(135deg, #022A73 0%, #0345BF 55%, #033A9F 100%)",
      }}
    >
      {/* Mesh gradient base */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(55% 55% at 12% 15%, rgba(79,179,194,0.28) 0%, transparent 65%)," +
            "radial-gradient(50% 55% at 88% 90%, rgba(214,241,245,0.14) 0%, transparent 65%)," +
            "radial-gradient(90% 70% at 50% 50%, rgba(11,23,48,0.35) 0%, transparent 75%)",
        }}
      />

      {/* Grelha subtil */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 opacity-[0.05]"
        style={{
          backgroundImage:
            "linear-gradient(rgba(250,251,253,0.6) 1px, transparent 1px), linear-gradient(90deg, rgba(250,251,253,0.6) 1px, transparent 1px)",
          backgroundSize: "56px 56px",
          maskImage:
            "radial-gradient(ellipse at center, black 30%, transparent 80%)",
        }}
      />

      {/* Linhas superior/inferior */}
      <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-accent-400/60 to-transparent" />
      <div className="absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-accent-400/40 to-transparent" />

      <div className="relative mx-auto grid max-w-6xl gap-14 px-4 py-24 md:py-28 lg:grid-cols-[1.05fr_1fr] lg:items-center lg:gap-16">
        {/* Coluna de conteúdo */}
        <div className="text-center lg:text-left">
          <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-sand-50/25 bg-sand-50/10 px-3.5 py-1.5 text-[11px] font-semibold uppercase tracking-[0.14em] text-sand-50 backdrop-blur-sm">
            <span className="relative flex h-1.5 w-1.5">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-accent-400/80" />
              <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-accent-400" />
            </span>
            Torna-te sócio
          </div>

          <h2 className="font-display text-4xl font-bold leading-[1.05] tracking-tight text-white md:text-5xl lg:text-[3.5rem]">
            {title}
          </h2>

          {subtitle && (
            <p className="mx-auto mt-6 max-w-xl text-base leading-relaxed text-sand-50/90 md:text-lg lg:mx-0">
              {subtitle}
            </p>
          )}

          <div className="mt-10 flex flex-col items-center gap-3 sm:flex-row sm:gap-4 lg:items-start lg:justify-start">
            <Link
              href={href}
              className="group relative inline-flex w-full items-center justify-center gap-2 overflow-hidden rounded-full bg-sand-50 px-7 py-3.5 text-sm font-semibold text-brand-700 shadow-[0_14px_45px_-12px_rgba(2,42,115,0.55)] ring-1 ring-inset ring-ink-900/5 transition-all duration-300 hover:bg-white hover:text-brand-800 hover:shadow-[0_22px_60px_-15px_rgba(2,42,115,0.75)] sm:w-auto"
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
              className="group inline-flex w-full items-center justify-center gap-1.5 rounded-full border border-sand-50/30 bg-sand-50/5 px-6 py-3.5 text-sm font-medium text-sand-50 backdrop-blur-sm transition-all duration-300 hover:border-accent-400/70 hover:bg-sand-50/10 hover:text-accent-400 sm:w-auto"
            >
              Saber mais sobre a BVA
              <ArrowRight
                className="h-3.5 w-3.5 opacity-0 -translate-x-1 transition-all duration-300 group-hover:opacity-100 group-hover:translate-x-0"
                strokeWidth={2.5}
                aria-hidden
              />
            </Link>
          </div>

          <p className="mt-8 text-xs text-sand-50/60 lg:text-left">
            Processo simples · Aprovação rápida · Sem compromisso
          </p>
        </div>

        {/* Grelha de benefícios */}
        <div className="relative">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {benefits.map(({ icon, label: benefitLabel, description }, index) => (
              <div
                key={benefitLabel}
                className={
                  "group relative overflow-hidden rounded-2xl border border-sand-50/15 bg-sand-50/[0.06] p-5 backdrop-blur-sm transition-all duration-300 hover:-translate-y-0.5 hover:border-accent-400/60 hover:bg-sand-50/[0.11] " +
                  (index % 2 === 1 ? "sm:mt-6" : "")
                }
              >
                <div
                  aria-hidden
                  className="pointer-events-none absolute -inset-px rounded-2xl opacity-0 transition-opacity duration-300 group-hover:opacity-100"
                  style={{
                    background:
                      "radial-gradient(120px 80px at 30% 0%, rgba(79,179,194,0.22), transparent 70%)",
                  }}
                />

                <div className="relative">
                  <div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-accent-400/30 to-accent-400/5 ring-1 ring-accent-400/40 transition-transform duration-300 group-hover:scale-105">
                    <AreaIcon name={icon} className="h-5 w-5 text-accent-400" />
                  </div>
                  <h3 className="text-[15px] font-semibold text-white">
                    {benefitLabel}
                  </h3>
                  <p className="mt-1 text-[13px] leading-snug text-sand-50/75">
                    {description}
                  </p>
                </div>
              </div>
            ))}
          </div>

          <div
            aria-hidden
            className="pointer-events-none absolute -inset-8 -z-10 rounded-[2rem] bg-gradient-to-br from-accent-400/15 via-transparent to-sand-50/5 blur-2xl"
          />
        </div>
      </div>
    </section>
  );
}
