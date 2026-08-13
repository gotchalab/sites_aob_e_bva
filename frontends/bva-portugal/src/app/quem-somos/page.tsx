import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import { ArrowRight, Award, Feather, HeartHandshake, Landmark, ShieldCheck } from "lucide-react";
import { api, parseHomeConfig } from "@/lib/api";
import { StatsBar } from "@/components/home/stats-bar";
import { AreasGrid } from "@/components/home/areas-grid";
import { JoinCta } from "@/components/home/join-cta";

export const revalidate = 300;

export const metadata: Metadata = {
  title: "Quem somos",
  description:
    "A BVA Portugal — Associação Técnica Portuguesa de Agapornis — reúne criadores dedicados à criação técnica, aos standards internacionais e às exposições especializadas.",
};

const VALUES = [
  {
    icon: Award,
    title: "Rigor técnico",
    body: "Trabalhamos com os standards internacionais reconhecidos, garantindo julgamento consistente e uniforme em todas as edições BVA.",
  },
  {
    icon: HeartHandshake,
    title: "Comunidade e partilha",
    body: "Ligamos criadores, juízes e especialistas, criando espaço para troca de experiência e conhecimento técnico.",
  },
  {
    icon: ShieldCheck,
    title: "Boas práticas",
    body: "Promovemos a criação responsável, o bem-estar das aves e a anilhagem oficial — garantia de rastreabilidade e alternativa à captura na natureza.",
  },
  {
    icon: Landmark,
    title: "Tradição em Portugal",
    body: "Décadas de história dedicadas à criação técnica de Agapornis em Portugal, com edições BVA Masters de referência.",
  },
] as const;

export default async function QuemSomosPage() {
  const site = await api.site();
  const home = parseHomeConfig(site);

  const missionParagraphs = (home.mission ?? "")
    .split(/\n{2,}|\n/)
    .map((p) => p.trim())
    .filter((p) => p.length > 0);

  const foundedLine = home.foundedYear ? `Fundada em ${home.foundedYear}` : null;
  const membersLine = home.memberCount
    ? `${home.memberCount.toLocaleString("pt-PT")} sócios ativos`
    : null;
  const meta = [foundedLine, membersLine].filter(Boolean).join(" · ");

  return (
    <>
      <section className="hero-pattern border-b border-sand-300">
        <div className="mx-auto max-w-6xl px-4 py-14 md:py-20">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white/70 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700 backdrop-blur-sm">
            <span className="h-1.5 w-1.5 rounded-full bg-brand-500" />
            A associação
          </div>
          <h1 className="font-display text-4xl font-bold leading-[1.05] tracking-tight text-ink-900 md:text-6xl">
            Quem somos
          </h1>
          <p className="mt-6 max-w-2xl text-lg leading-relaxed text-ink-800/85 md:text-xl">
            A BVA Portugal — associação técnica portuguesa de Agapornis — junta
            criadores, juízes e especialistas dedicados à criação, aos standards
            internacionais e às exposições técnicas destas aves.
          </p>
          {meta && (
            <p className="mt-4 text-xs uppercase tracking-widest text-ink-500">{meta}</p>
          )}
        </div>
      </section>

      <section className="border-b border-sand-300 bg-white">
        <div className="mx-auto max-w-6xl px-4 py-16 md:py-24">
          <div className="grid gap-12 md:grid-cols-[1fr,1fr] md:items-start md:gap-16">
            <div>
              <div className="mb-4 text-[11px] font-semibold uppercase tracking-[0.2em] text-brand-600">
                A nossa missão
              </div>
              <h2 className="font-display text-3xl font-semibold leading-[1.15] tracking-tight text-ink-900 md:text-[2.5rem]">
                Promover a criação técnica com rigor e paixão.
              </h2>
              <div className="mt-8 space-y-4 text-[15px] leading-relaxed text-ink-800/80 md:text-base">
                {missionParagraphs.length > 0 ? (
                  missionParagraphs.map((p, i) => <p key={i}>{p}</p>)
                ) : (
                  <>
                    <p>
                      Somos a associação técnica portuguesa de Agapornis, dedicada
                      à criação responsável, ao julgamento por standards
                      internacionais e à divulgação técnica destas aves. Ao longo
                      de décadas construímos uma comunidade que junta tradição,
                      técnica e partilha de conhecimento.
                    </p>
                    <p>
                      Emitimos anilhas oficiais para os nossos sócios, organizamos
                      as edições BVA Masters, participamos em concursos
                      internacionais e mantemos uma rede activa de comunicação
                      entre criadores portugueses e europeus.
                    </p>
                  </>
                )}
              </div>
              <div className="mt-10 flex flex-wrap gap-3">
                <Link
                  href="/inscricao-socio"
                  className="inline-flex items-center gap-2 rounded-full bg-brand-500 px-6 py-3 text-sm font-semibold text-white shadow-sm ring-1 ring-brand-600/20 transition hover:bg-brand-600 hover:shadow-md"
                >
                  Quero ser sócio
                  <ArrowRight className="h-4 w-4" strokeWidth={2.5} aria-hidden />
                </Link>
                <Link
                  href="/contacto"
                  className="inline-flex items-center rounded-full border border-ink-900/15 bg-white/80 px-6 py-3 text-sm font-semibold text-ink-800 transition hover:border-brand-500 hover:text-brand-700"
                >
                  Falar connosco
                </Link>
              </div>
            </div>

            <MissionAside missionImageUrl={home.missionImageUrl} missionQuote={home.missionQuote} />
          </div>
        </div>
      </section>

      <StatsBar home={home} />

      <section className="border-b border-sand-300 bg-sand-100/60">
        <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
          <div className="mb-12 md:text-center">
            <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
              Os nossos valores
            </div>
            <h2 className="font-display text-3xl font-semibold leading-tight text-ink-900 md:text-4xl">
              O que nos define enquanto associação.
            </h2>
          </div>

          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            {VALUES.map(({ icon: Icon, title, body }) => (
              <article
                key={title}
                className="flex h-full flex-col rounded-2xl border border-black/5 bg-white p-6 shadow-sm"
              >
                <div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600">
                  <Icon className="h-5 w-5" strokeWidth={1.5} aria-hidden />
                </div>
                <h3 className="font-display text-lg font-semibold leading-tight text-ink-900">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-relaxed text-ink-700/80">{body}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <AreasGrid home={home} />

      <JoinCta home={home} />
    </>
  );
}

function MissionAside({ missionImageUrl, missionQuote }: { missionImageUrl?: string | null; missionQuote?: string | null }) {
  if (missionImageUrl) {
    return (
      <div className="relative aspect-[5/4] w-full overflow-hidden rounded-md bg-sand-200 shadow-sm ring-1 ring-black/5">
        <Image
          src={missionImageUrl}
          alt=""
          fill
          sizes="(min-width: 768px) 50vw, 100vw"
          className="object-cover"
        />
      </div>
    );
  }

  return (
    <aside className="md:pt-4">
      <div className="border-l-2 border-brand-500 pl-6 md:pl-8">
        <Feather className="h-6 w-6 text-brand-500" strokeWidth={1.5} aria-hidden />
        <p className="mt-5 font-display text-xl leading-snug text-ink-900 md:text-2xl">
          {missionQuote ?? (
            <>
              Décadas dedicadas à criação de Agapornis —
              <span className="text-ink-700/70">
                {" "}
                elevando standards técnicos, exposições e a partilha entre criadores em Portugal.
              </span>
            </>
          )}
        </p>
        <p className="mt-6 text-sm leading-relaxed text-ink-700/80">
          As edições BVA Masters são hoje um dos concursos técnicos de referência
          para Agapornis em Portugal, com julgamento por painel internacional e
          participação de criadores de vários países europeus.
        </p>
      </div>
    </aside>
  );
}
