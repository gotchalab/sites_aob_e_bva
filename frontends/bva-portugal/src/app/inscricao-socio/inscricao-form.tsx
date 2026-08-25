"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { SignaturePad, type SignaturePadHandle } from "@/components/signature/signature-pad";
import { SignatureFullscreenModal } from "@/components/signature/signature-fullscreen-modal";

const IBAN = "PT50 0010 0000 3704 9640 0014 1";
const TITULAR = "Associação Ornitológica de Barcelos";
const QUOTA_ANUAL_BVA_PORTUGAL = 40;
const MAX_COMPROVATIVO_MB = 8;

const TURNSTILE_SITEKEY = process.env.NEXT_PUBLIC_TURNSTILE_SITEKEY;

declare global {
  interface Window {
    turnstile?: {
      render: (
        el: string | HTMLElement,
        opts: { sitekey: string; callback: (token: string) => void; theme?: string },
      ) => string;
      reset: (id?: string) => void;
    };
  }
}

const inputCls =
  "mt-1 w-full rounded-lg border bg-white px-3.5 py-2.5 text-ink-900 placeholder-ink-500/60 shadow-sm transition focus:outline-none focus:ring-2 focus:ring-brand-500/20";
const inputBorder = (err: boolean) =>
  err ? "border-red-500 focus:border-red-500" : "border-ink-900/15 focus:border-brand-500";
const labelCls =
  "text-[11px] font-medium uppercase tracking-widest text-ink-500";
const sectionCls =
  "rounded-2xl border border-sand-300 bg-white p-5 shadow-sm md:p-7";
const sectionTitleCls =
  "font-display text-lg font-semibold text-ink-900 md:text-xl";
const sectionHintCls = "mt-1 text-sm text-ink-500";
const errCls = "mt-1 text-xs text-red-600";

export function InscricaoForm({
  siteName,
  contactEmail,
}: {
  siteName: string;
  contactEmail: string | null;
}) {
  const router = useRouter();
  const [fotoFile, setFotoFile] = useState<File | undefined>();
  const [fotoPreview, setFotoPreview] = useState<string | undefined>();
  const signaturePadRef = useRef<SignaturePadHandle>(null);
  const [signatureEmpty, setSignatureEmpty] = useState(true);
  const [signatureFullscreen, setSignatureFullscreen] = useState(false);
  const [comprovativoFile, setComprovativoFile] = useState<File | undefined>();
  const [tipoSocio, setTipoSocio] = useState<"apoiante" | "criador" | "">("");
  const [stamFonpValue, setStamFonpValue] = useState("");
  const [stamBvaValue, setStamBvaValue] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const firstInvalidRef = useRef<string | null>(null);
  const [state, setState] = useState<"idle" | "sending" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();
  const [token, setToken] = useState<string>();
  const widgetContainer = useRef<HTMLDivElement>(null);
  const widgetId = useRef<string | undefined>(undefined);

  const totalDevido = QUOTA_ANUAL_BVA_PORTUGAL;

  function onFieldInvalid(e: React.FormEvent<HTMLInputElement | HTMLSelectElement>) {
    e.preventDefault();
    const name = e.currentTarget.name;
    const msg = e.currentTarget.validity.valueMissing ? "Campo obrigatório" : "Formato inválido";
    if (!firstInvalidRef.current) {
      firstInvalidRef.current = name;
      requestAnimationFrame(() => {
        const el = document.querySelector(`[name="${firstInvalidRef.current}"]`);
        el?.scrollIntoView({ behavior: "smooth", block: "center" });
        firstInvalidRef.current = null;
      });
    }
    setFieldErrors(prev => ({ ...prev, [name]: msg }));
  }

  function clearField(name: string) {
    setFieldErrors(prev => { const { [name]: _, ...rest } = prev; return rest; });
  }

  const err = (name: string) =>
    fieldErrors[name] ? <p className={errCls}>{fieldErrors[name]}</p> : null;

  useEffect(() => {
    if (!TURNSTILE_SITEKEY || !widgetContainer.current) return;
    let cancelled = false;
    const mount = () => {
      if (cancelled || !widgetContainer.current || !window.turnstile) return;
      if (widgetId.current) return;
      widgetId.current = window.turnstile.render(widgetContainer.current, {
        sitekey: TURNSTILE_SITEKEY,
        theme: "light",
        callback: (t) => setToken(t),
      });
    };
    if (window.turnstile) mount();
    else {
      const s = document.createElement("script");
      s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js";
      s.async = true;
      s.defer = true;
      s.onload = mount;
      document.head.appendChild(s);
    }
    return () => { cancelled = true; };
  }, []);

  async function onFotoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) { setFotoFile(undefined); setFotoPreview(undefined); return; }
    if (file.size > 4 * 1024 * 1024) { setErrorMsg("Foto: máximo 4 MB."); e.target.value = ""; return; }
    setFotoFile(file);
    setFotoPreview(await readAsDataUrl(file));
  }

  function onComprovativoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) { setComprovativoFile(undefined); return; }
    if (file.size > MAX_COMPROVATIVO_MB * 1024 * 1024) {
      setErrorMsg(`Comprovativo: máximo ${MAX_COMPROVATIVO_MB} MB.`);
      e.target.value = "";
      return;
    }
    setComprovativoFile(file);
    clearField("comprovativo");
  }

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const assinaturaEmpty = signaturePadRef.current?.isEmpty() ?? true;
    if (assinaturaEmpty) {
      setFieldErrors(prev => ({ ...prev, assinatura: "Campo obrigatório" }));
      document.getElementById("assinatura-anchor")?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    const assinatura = signaturePadRef.current?.toDataUrl() ?? "";
    if (!comprovativoFile) {
      setFieldErrors(prev => ({ ...prev, comprovativo: "Campo obrigatório" }));
      document.getElementById("comprovativo-anchor")?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }

    setState("sending");
    setErrorMsg(undefined);
    const raw = new FormData(e.currentTarget);

    const fd = new FormData();
    const strFields = [
      "nomeCompleto", "email", "telefone", "cartaoCidadao", "nif", "nacionalidade",
      "dataNascimento", "estadoCivil", "morada", "moradaLinha2", "codigoPostal",
      "localidade", "profissao", "stamFonp", "stamFonpNumero", "stamBva", "stamBvaNumero", "notas",
    ];
    for (const k of strFields) {
      const v = String(raw.get(k) ?? "").trim();
      if (v) fd.append(k, v);
    }
    fd.append("socioApoiante", tipoSocio === "apoiante" ? "true" : "false");
    fd.append("socioCriador", tipoSocio === "criador" ? "true" : "false");
    fd.append("socioBvaPortugal", "true");
    fd.append("aceitouRegulamento", "true");
    if (token) fd.append("turnstileToken", token);

    if (fotoFile) fd.append("foto", fotoFile, fotoFile.name);
    fd.append("assinatura", dataUrlToBlob(assinatura), "assinatura.png");
    fd.append("comprovativoPagamento", comprovativoFile, comprovativoFile.name);

    const res = await api.submitInscricaoSocio(fd);
    if (res.ok) {
      router.push(`/inscricao-socio/obrigado?id=${res.submissionId ?? ""}`);
      return;
    }
    setState("error");
    setErrorMsg(res.error ?? "Erro desconhecido");
    if (window.turnstile && widgetId.current) window.turnstile.reset(widgetId.current);
    setToken(undefined);
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-6">
      {/* DADOS PESSOAIS */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>1. Dados pessoais</h2>
        <p className={sectionHintCls}>Todos os campos marcados com * são obrigatórios.</p>

        <div className="mt-5 grid gap-4 md:grid-cols-2">
          <label className="block md:col-span-2">
            <span className={labelCls}>Nome completo *</span>
            <input
              required name="nomeCompleto" autoComplete="name"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["nomeCompleto"])}`}
            />
            {err("nomeCompleto")}
          </label>
          <label className="block">
            <span className={labelCls}>Cartão de Cidadão / BI *</span>
            <input
              required name="cartaoCidadao" inputMode="numeric"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["cartaoCidadao"])}`}
            />
            {err("cartaoCidadao")}
          </label>
          <label className="block">
            <span className={labelCls}>NIF *</span>
            <input
              required name="nif" inputMode="numeric"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["nif"])}`}
            />
            {err("nif")}
          </label>
          <label className="block">
            <span className={labelCls}>Nacionalidade</span>
            <input name="nacionalidade" defaultValue="Portuguesa" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block">
            <span className={labelCls}>Data de nascimento</span>
            <input type="date" name="dataNascimento" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block">
            <span className={labelCls}>Estado civil</span>
            <select name="estadoCivil" defaultValue="" className={`${inputCls} border-ink-900/15 focus:border-brand-500`}>
              <option value="">— selecionar —</option>
              <option value="Solteiro">Solteiro/a</option>
              <option value="Casado">Casado/a</option>
              <option value="Divorciado">Divorciado/a</option>
              <option value="Viuvo">Viúvo/a</option>
            </select>
          </label>
          <label className="block">
            <span className={labelCls}>Telefone</span>
            <input name="telefone" type="tel" inputMode="tel" autoComplete="tel" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block md:col-span-2">
            <span className={labelCls}>Email *</span>
            <input
              required type="email" name="email" autoComplete="email"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["email"])}`}
            />
            {err("email")}
          </label>
          <label className="block md:col-span-2">
            <span className={labelCls}>Morada *</span>
            <input
              required name="morada" autoComplete="street-address"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["morada"])}`}
            />
            {err("morada")}
          </label>
          <label className="block md:col-span-2">
            <span className={labelCls}>Morada (linha 2)</span>
            <input name="moradaLinha2" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block">
            <span className={labelCls}>Código postal *</span>
            <input
              required name="codigoPostal" autoComplete="postal-code"
              onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["codigoPostal"])}`}
            />
            {err("codigoPostal")}
          </label>
          <label className="block">
            <span className={labelCls}>Localidade</span>
            <input name="localidade" autoComplete="address-level2" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block md:col-span-2">
            <span className={labelCls}>Profissão</span>
            <input name="profissao" className={`${inputCls} border-ink-900/15 focus:border-brand-500`} />
          </label>
          <label className="block md:col-span-2">
            <span className={labelCls}>Foto (opcional)</span>
            <input
              type="file" accept="image/png,image/jpeg,image/webp" onChange={onFotoChange}
              className="mt-1 block w-full text-sm text-ink-700 file:mr-4 file:rounded-full file:border-0 file:bg-brand-500/10 file:px-4 file:py-2 file:text-sm file:font-medium file:text-brand-700 hover:file:bg-brand-500/20"
            />
            {fotoPreview && (
              <div className="mt-3 flex items-center gap-3">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={fotoPreview} alt="Pré-visualização" className="h-20 w-20 rounded-lg border object-cover" />
                <button type="button" onClick={() => { setFotoFile(undefined); setFotoPreview(undefined); }} className="text-xs text-red-600 underline">
                  Remover
                </button>
              </div>
            )}
          </label>
        </div>
      </section>

      {/* TIPO DE SOCIO */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>2. Tipo de sócio {siteName}</h2>
        <div className={`mt-4 flex flex-col gap-3 rounded-lg p-3 transition ${fieldErrors["tipoSocio"] ? "border border-red-400 bg-red-50/40" : "border border-transparent"}`}>
          <label className="inline-flex cursor-pointer items-start gap-3">
            <input
              required type="radio" name="tipoSocio" value="apoiante"
              checked={tipoSocio === "apoiante"}
              onInvalid={onFieldInvalid}
              onChange={() => { setTipoSocio("apoiante"); setStamFonpValue(""); clearField("tipoSocio"); }}
              className="mt-1 h-4 w-4 accent-brand-500"
            />
            <span>
              <span className="font-medium text-ink-900">Sócio Apoiante</span>
              <span className="block text-sm text-ink-500">Quero apoiar a associação. Não pretendo criar aves para concursos nem pedir anilhas federadas.</span>
            </span>
          </label>
          <label className="inline-flex cursor-pointer items-start gap-3">
            <input
              required type="radio" name="tipoSocio" value="criador"
              checked={tipoSocio === "criador"}
              onInvalid={onFieldInvalid}
              onChange={() => { setTipoSocio("criador"); clearField("tipoSocio"); }}
              className="mt-1 h-4 w-4 accent-brand-500"
            />
            <span>
              <span className="font-medium text-ink-900">Sócio Criador</span>
              <span className="block text-sm text-ink-500">Crio aves e pretendo pedir anilhas federadas. É necessário ter número STAM da federação (FONP).</span>
            </span>
          </label>
        </div>
        {err("tipoSocio")}
        {tipoSocio === "criador" && (
          <div className="mt-5 grid gap-4 md:grid-cols-2">
            <label className="block">
              <span className={labelCls}>Já tem número STAM? *</span>
              <select
                required name="stamFonp" value={stamFonpValue}
                onInvalid={onFieldInvalid}
                onChange={(e) => { setStamFonpValue(e.target.value); clearField(e.currentTarget.name); }}
                className={`${inputCls} ${inputBorder(!!fieldErrors["stamFonp"])}`}
              >
                <option value="">— selecionar —</option>
                <option value="Sim">Sim, já tenho</option>
                <option value="Nao">Não tenho</option>
              </select>
              {err("stamFonp")}
            </label>
            {stamFonpValue === "Sim" && (
              <label className="block">
                <span className={labelCls}>Qual é o seu Nº STAM? *</span>
                <input
                  required name="stamFonpNumero"
                  onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
                  className={`${inputCls} ${inputBorder(!!fieldErrors["stamFonpNumero"])}`}
                />
                {err("stamFonpNumero")}
              </label>
            )}
          </div>
        )}
      </section>

      {/* BVA INTERNACIONAL */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>3. BVA Internacional</h2>
        <p className={sectionHintCls}>
          Como sócio da BVA Portugal, passa automaticamente a ser sócio da BVA Internacional (Bélgica).
        </p>
        <div className="mt-4">
          <label className="block">
            <span className={labelCls}>Já é sócio da BVA Internacional por outro clube afiliado? *</span>
            <select
              required name="stamBva" value={stamBvaValue}
              onInvalid={onFieldInvalid}
              onChange={(e) => { setStamBvaValue(e.target.value); clearField(e.currentTarget.name); }}
              className={`${inputCls} mt-2 ${inputBorder(!!fieldErrors["stamBva"])}`}
            >
              <option value="">— selecionar —</option>
              <option value="Nao">Não</option>
              <option value="Sim">Sim</option>
            </select>
            {err("stamBva")}
          </label>
          {stamBvaValue === "Sim" && (
            <label className="mt-4 block">
              <span className={labelCls}>Qual é o seu número de sócio BVA Internacional? *</span>
              <input
                required name="stamBvaNumero"
                onInvalid={onFieldInvalid} onChange={(e) => clearField(e.currentTarget.name)}
                className={`${inputCls} ${inputBorder(!!fieldErrors["stamBvaNumero"])}`}
              />
              {err("stamBvaNumero")}
            </label>
          )}
        </div>
      </section>

      {/* NOTAS */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>4. Notas adicionais (opcional)</h2>
        <textarea
          name="notas" rows={4}
          placeholder="Alguma informação adicional que queira partilhar com a Direcção..."
          className={`${inputCls} resize-y border-ink-900/15 focus:border-brand-500`}
        />
      </section>

      {/* DECLARACAO + ASSINATURA */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>5. Declaração e assinatura</h2>
        <p className="mt-2 text-sm leading-relaxed text-ink-700">
          Declaro que todos os dados são verdadeiros e que tomei conhecimento do
          Regulamento Geral Interno, o qual me comprometo a cumprir, pelo que solicito a
          minha admissão como associado.
        </p>
        <div className="mt-4">
          <label className="inline-flex cursor-pointer items-start gap-3">
            <input
              required type="checkbox" name="aceitouRegulamento"
              onInvalid={onFieldInvalid}
              onChange={(e) => { if (e.currentTarget.checked) clearField("aceitouRegulamento"); }}
              className="mt-1 h-4 w-4 accent-brand-500"
            />
            <span className="font-medium text-ink-900">
              Li e aceito os{" "}
              <a href={`${process.env.NEXT_PUBLIC_AOB_SITE_URL ?? "http://localhost:3000"}/artigos/estatutos`} target="_blank" rel="noopener noreferrer" className="underline hover:text-brand-700">
                Estatutos
              </a>{" "}*
            </span>
          </label>
          {err("aceitouRegulamento")}
        </div>
        <div id="assinatura-anchor" className="mt-5">
          <span className={labelCls}>Assinatura *</span>
          <p className="mt-1 text-xs text-ink-500">
            Assine no espaço abaixo com o dedo (mobile) ou com o rato.
          </p>
          <div className="mt-2">
            <SignaturePad
              ref={signaturePadRef}
              error={!!fieldErrors["assinatura"]}
              onEmptyChange={(empty) => {
                setSignatureEmpty(empty);
                if (!empty) clearField("assinatura");
              }}
            />
          </div>
          <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => setSignatureFullscreen(true)}
              className="inline-flex min-h-[44px] items-center justify-center gap-2 rounded-full border border-ink-900/15 bg-white px-4 py-2 text-sm font-medium text-ink-800 transition hover:border-brand-500 hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
            >
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M8 3H5a2 2 0 0 0-2 2v3" />
                <path d="M21 8V5a2 2 0 0 0-2-2h-3" />
                <path d="M3 16v3a2 2 0 0 0 2 2h3" />
                <path d="M16 21h3a2 2 0 0 0 2-2v-3" />
              </svg>
              Ampliar para assinar
            </button>
            <button
              type="button"
              disabled={signatureEmpty}
              onClick={() => {
                signaturePadRef.current?.clear();
                setSignatureEmpty(true);
              }}
              className="inline-flex min-h-[44px] items-center justify-center gap-2 rounded-full border border-ink-900/15 bg-white px-4 py-2 text-sm font-medium text-ink-700 transition hover:border-red-300 hover:bg-red-50 hover:text-red-700 focus:outline-none focus:ring-2 focus:ring-red-400/30 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-ink-900/15 disabled:hover:bg-white disabled:hover:text-ink-700"
            >
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M3 6h18" />
                <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                <path d="m19 6-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
              </svg>
              Limpar assinatura
            </button>
          </div>
          {err("assinatura")}
        </div>
        <SignatureFullscreenModal
          open={signatureFullscreen}
          initialVector={signatureEmpty ? null : signaturePadRef.current?.getVectorData() ?? null}
          onCancel={() => setSignatureFullscreen(false)}
          onConfirm={(v) => {
            signaturePadRef.current?.setVectorData(v);
            setSignatureEmpty(false);
            clearField("assinatura");
            setSignatureFullscreen(false);
          }}
        />
      </section>

      {/* PAGAMENTO */}
      <section className={sectionCls} id="comprovativo-anchor">
        <h2 className={sectionTitleCls}>6. Pagamento</h2>
        <p className={sectionHintCls}>
          Para submeter o pedido, é necessário efectuar o pagamento e anexar o comprovativo.
        </p>

        <div className="mt-4 rounded-xl border border-sand-300 bg-sand-100 p-4 text-sm text-ink-900">
          <div>
            <div className="text-xs font-medium uppercase tracking-widest text-ink-500">Quota anual (AOB + BVA Portugal)</div>
            <div className="mt-0.5 text-base font-semibold">{QUOTA_ANUAL_BVA_PORTUGAL.toFixed(2)} €</div>
            <div className="text-xs text-ink-500">Quota única que cobre AOB, BVA Portugal e BVA Internacional</div>
          </div>
          <div className="mt-3 flex items-center justify-between border-t border-sand-300 pt-3">
            <span className="text-sm font-medium text-ink-900">Total a pagar agora</span>
            <span className="text-lg font-bold text-brand-700">{totalDevido.toFixed(2)} €</span>
          </div>
        </div>

        <div className="mt-4 rounded-lg border border-sand-300 bg-white p-4 text-sm text-ink-900">
          <p className="text-xs font-medium uppercase tracking-widest text-ink-500">Dados para transferência</p>
          <dl className="mt-2 grid gap-1 sm:grid-cols-[auto,1fr] sm:gap-x-4">
            <dt className="text-ink-500">Titular:</dt>
            <dd className="font-medium">{TITULAR}</dd>
            <dt className="text-ink-500">IBAN:</dt>
            <dd className="font-mono text-[13px]">{IBAN}</dd>
          </dl>
          <button
            type="button"
            onClick={() => navigator.clipboard?.writeText(IBAN.replace(/\s/g, ""))}
            className="mt-3 inline-flex items-center gap-1.5 rounded-full border border-brand-500/40 bg-white px-3 py-1 text-xs font-medium text-brand-700 shadow-sm transition hover:bg-brand-500/10"
          >
            Copiar IBAN
          </button>
        </div>

        <label className="mt-5 block">
          <span className={labelCls}>Comprovativo de pagamento *</span>
          <input
            type="file" accept="application/pdf,image/png,image/jpeg,image/webp"
            onChange={onComprovativoChange}
            className="mt-1 block w-full text-sm text-ink-700 file:mr-4 file:rounded-full file:border-0 file:bg-brand-500/10 file:px-4 file:py-2 file:text-sm file:font-medium file:text-brand-700 hover:file:bg-brand-500/20"
          />
          <span className="mt-1 block text-xs text-ink-500">
            PDF ou imagem (PNG/JPEG/WebP). Máximo {MAX_COMPROVATIVO_MB} MB.
          </span>
          {err("comprovativo")}
          {comprovativoFile && (
            <div className="mt-3 flex items-center gap-3 rounded-lg border border-sand-300 bg-sand-100 p-3">
              <span className="text-2xl">📎</span>
              <div className="flex-1 min-w-0">
                <div className="truncate text-sm font-medium text-ink-900">{comprovativoFile.name}</div>
                <div className="text-xs text-ink-500">
                  {(comprovativoFile.size / 1024).toFixed(0)} KB · {comprovativoFile.type || "ficheiro"}
                </div>
              </div>
              <button type="button" onClick={() => setComprovativoFile(undefined)} className="text-xs text-red-600 underline">
                Remover
              </button>
            </div>
          )}
        </label>
      </section>

      {TURNSTILE_SITEKEY ? (
        <div ref={widgetContainer} />
      ) : (
        <p className="rounded-lg border border-amber-300/60 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          NEXT_PUBLIC_TURNSTILE_SITEKEY não configurado — modo dev sem verificação anti-bot.
        </p>
      )}

      {state === "error" && errorMsg && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          {errorMsg}
        </div>
      )}

      <button
        type="submit" disabled={state === "sending"}
        className="rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {state === "sending" ? "A enviar..." : "Submeter proposta de admissão"}
      </button>

      {contactEmail && (
        <p className="text-center text-xs text-ink-500">
          Em caso de dúvida contacte{" "}
          <a className="underline hover:text-brand-700" href={`mailto:${contactEmail}`}>
            {contactEmail}
          </a>
        </p>
      )}
    </form>
  );
}

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result ?? ""));
    r.onerror = () => reject(r.error);
    r.readAsDataURL(file);
  });
}

function dataUrlToBlob(dataUrl: string): Blob {
  const [header, base64] = dataUrl.split(",", 2);
  const match = header?.match(/^data:([^;]+);base64$/i);
  const mime = match?.[1] ?? "application/octet-stream";
  const bin = atob(base64 ?? "");
  const arr = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
  return new Blob([arr], { type: mime });
}
