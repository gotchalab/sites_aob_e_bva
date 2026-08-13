import type { Metadata } from "next";
import Link from "next/link";
import Image from "next/image";
import { ArrowRight, Feather, HeartHandshake, Landmark, ShieldCheck } from "lucide-react";
import { api, parseHomeConfig } from "@/lib/api";
import { StatsBar } from "@/components/home/stats-bar";
import { AreasGrid } from "@/components/home/areas-grid";
import { JoinCta } from "@/components/home/join-cta";

export const revalidate = 300;

export const metadata: Metadata = {
  title: "Quem somos",
  description:
    "A Associação Ornitológica de Barcelos reúne criadores e apaixonados pela ornitologia, promovendo a criação responsável, as exposições de aves e as boas práticas na região.",
};

const VALUES = [
  {
    icon: Landmark,
    title: "Tradição em Barcelos",
    body: "Décadas de história ao serviço dos criadores da região, com raízes profundas na cultura ornitológica local.",
  },
  {
    icon: HeartHandshake,
    title: "Comunidade e partilha",
    body: "Um espaço para trocar experiências, aprender com sócios veteranos e apoiar quem está a começar.",
  },
  {
    icon: ShieldCheck,
    title: "Boas práticas",
    body: "Promovemos a criação responsável, o bem-estar das aves e a anilhagem oficial — garantia de rastreabilidade e alternativa à captura na natureza.",
  },
  {
    icon: Feather,
    title: "Divulgação da ornitologia",
    body: "Organizamos exposições e eventos que elevam o nível técnico dos criadores e apresentam a ornitologia ao público da região.",
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
      <section className="hero-pattern border-b border-gold-500/30">
        <div className="mx-auto max-w-6xl px-4 py-14 md:py-20">
          <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white/70 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700 backdrop-blur-sm">
            <span className="h-1.5 w-1.5 rounded-full bg-brand-500" />
            A associação
          </div>
          <h1 className="font-display text-4xl font-bold leading-[1.05] tracking-tight text-earth-900 md:text-6xl">
            Quem somos
          </h1>
          <p className="mt-6 max-w-2xl text-lg leading-relaxed text-earth-800/85 md:text-xl">
            Somos a associação de referência para criadores de aves em Barcelos.
            Emitimos anilhas federadas, organizamos exposições, promovemos boas
            práticas e juntamos uma comunidade com décadas de tradição e paixão
            pela ornitologia.
          </p>
          {meta && (
            <p className="mt-4 text-xs uppercase tracking-widest text-earth-700/70">{meta}</p>
          )}
        </div>
      </section>

      <section className="border-b border-gold-500/20 bg-white">
        <div className="mx-auto max-w-6xl px-4 py-16 md:py-24">
          <div className="grid gap-12 md:grid-cols-[1fr,1fr] md:items-start md:gap-16">
            <div>
              <div className="mb-4 text-[11px] font-semibold uppercase tracking-[0.2em] text-brand-600">
                A nossa missão
              </div>
              <h2 className="font-display text-3xl font-semibold leading-[1.15] tracking-tight text-earth-900 md:text-[2.5rem]">
                Promover a ornitologia com rigor e paixão.
              </h2>
              <div className="mt-8 space-y-4 text-[15px] leading-relaxed text-earth-800/80 md:text-base">
                {missionParagraphs.length > 0 ? (
                  missionParagraphs.map((p, i) => <p key={i}>{p}</p>)
                ) : (
                  <>
                    <p>
                      A Associação Ornitológica de Barcelos nasceu da vontade
                      de criar um espaço de referência para os criadores de aves
                      da região. Ao longo de décadas, construímos uma comunidade
                      assente na partilha de conhecimento, na criação responsável
                      e no amor pelas aves.
                    </p>
                    <p>
                      Como sócio tens acesso a anilhas federadas com o teu código
                      único de criador, podes inscrever as tuas aves em exposições
                      e concursos com classificação oficial, contas com apoio
                      técnico de criadores experientes e beneficias de um portal
                      online para gerir pedidos e quotas.
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
                  className="inline-flex items-center rounded-full border border-earth-800/20 bg-white/80 px-6 py-3 text-sm font-semibold text-earth-800 transition hover:border-brand-500 hover:text-brand-700"
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

      <section className="border-b border-gold-500/20 bg-cream-100/40">
        <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
          <div className="mb-12 md:text-center">
            <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
              Os nossos valores
            </div>
            <h2 className="font-display text-3xl font-semibold leading-tight text-earth-900 md:text-4xl">
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
                <h3 className="font-display text-lg font-semibold leading-tight text-earth-900">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-relaxed text-earth-700/80">{body}</p>
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
      <div className="relative aspect-[5/4] w-full overflow-hidden rounded-md bg-cream-200 shadow-sm ring-1 ring-black/5">
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
        <p className="mt-5 font-display text-xl leading-snug text-earth-900 md:text-2xl">
          {missionQuote ?? (
            <>
              Na AOB encontras suporte técnico, anilhas federadas e uma comunidade unida —
              <span className="text-earth-800/70">
                {" "}
                tudo o que precisas para criar com confiança e progredir como criador.
              </span>
            </>
          )}
        </p>
        <p className="mt-6 text-sm leading-relaxed text-earth-700/80">
          A sede da associação abre semanalmente: vem buscar anilhas, esclarecer
          dúvidas técnicas ou simplesmente conviver com quem partilha a mesma
          paixão pelas aves.
        </p>
      </div>
    </aside>
  );
}
