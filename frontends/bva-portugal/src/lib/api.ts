import { API_URL, SITE_SLUG } from "./config";
import type {
  Announcement,
  ArticleDetail,
  ArticleSummary,
  CategoryTree,
  ContactRequest,
  ConvoyageActiveYearDto,
  DownloadDetail,
  DownloadSummary,
  FormSubmissionResponse,
  HomeConfig,
  InscricaoConvoyageRequest,
  MenuItem,
  PagedResult,
  Site,
  Sponsor,
} from "./api-types";

async function get<T>(path: string, revalidate = 300): Promise<T> {
  const res = await fetch(`${API_URL}/api/sites/${SITE_SLUG}${path}`, {
    next: { revalidate },
    headers: { Accept: "application/json" },
  });
  if (!res.ok) {
    throw new Error(`API ${res.status}: GET ${path}`);
  }
  return res.json() as Promise<T>;
}

async function tryGet<T>(path: string, revalidate: number = 300): Promise<T | null> {
  const init: RequestInit & { next?: { revalidate?: number } } =
    revalidate === 0
      ? { cache: "no-store", headers: { Accept: "application/json" } }
      : { next: { revalidate }, headers: { Accept: "application/json" } };
  const res = await fetch(`${API_URL}/api/sites/${SITE_SLUG}${path}`, init);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status}: GET ${path}`);
  return res.json() as Promise<T>;
}

export const api = {
  site: () => get<Site>("", 60),
  menu: (type = "mainmenu") => get<MenuItem[]>(`/menu?type=${encodeURIComponent(type)}`, 60),
  categories: (kind: "articles" | "downloads" = "articles") =>
    get<CategoryTree[]>(`/categories?kind=${kind}`),
  articles: (params: { category?: string; search?: string; page?: number; pageSize?: number; offset?: number; limit?: number; featured?: boolean } = {}) => {
    const q = new URLSearchParams();
    if (params.category) q.set("category", params.category);
    if (params.search) q.set("search", params.search);
    if (params.page) q.set("page", String(params.page));
    if (params.pageSize) q.set("pageSize", String(params.pageSize));
    if (params.offset !== undefined) q.set("offset", String(params.offset));
    if (params.limit !== undefined) q.set("limit", String(params.limit));
    if (params.featured) q.set("featured", "true");
    const qs = q.toString();
    return get<PagedResult<ArticleSummary>>(`/articles${qs ? `?${qs}` : ""}`);
  },
  article: (slug: string) => tryGet<ArticleDetail>(`/articles/${encodeURIComponent(slug)}`),
  sponsors: () => get<Sponsor[]>(`/sponsors`, 300),
  downloads: (params: { category?: string; search?: string; page?: number; pageSize?: number } = {}) => {
    const q = new URLSearchParams();
    if (params.category) q.set("category", params.category);
    if (params.search) q.set("search", params.search);
    if (params.page) q.set("page", String(params.page));
    if (params.pageSize) q.set("pageSize", String(params.pageSize));
    const qs = q.toString();
    return get<PagedResult<DownloadSummary>>(`/downloads${qs ? `?${qs}` : ""}`);
  },
  download: (slug: string) => tryGet<DownloadDetail>(`/downloads/${encodeURIComponent(slug)}`),
  submitContact: (body: ContactRequest) => post<FormSubmissionResponse>(`/forms/contact`, body),
  submitInscricaoSocio: (body: FormData) => postForm<FormSubmissionResponse>(`/forms/inscricao-socio`, body),
  submitInscricaoConvoyage: (body: InscricaoConvoyageRequest) => post<FormSubmissionResponse>(`/forms/inscricao-convoyage`, body),
  // Sem cache — o admin pode fechar/reabrir inscrições ou trocar ano ativo a
  // qualquer momento e a página pública tem de reflectir isso imediatamente.
  convoyageActiveYear: () => tryGet<ConvoyageActiveYearDto>(`/convoyage/active-year`, 0),
};

export function parseHomeConfig(site: Site | null | undefined): HomeConfig {
  if (!site?.homeConfig) return {};
  try {
    return JSON.parse(site.homeConfig) as HomeConfig;
  } catch {
    return {};
  }
}

export function parseAnnouncement(site: Site | null | undefined): Announcement | null {
  if (!site?.announcement) return null;
  try {
    const a = JSON.parse(site.announcement) as Announcement;
    if (!a.enabled) return null;
    if (typeof a.message !== "string" || a.message.trim().length === 0) return null;
    return a;
  } catch {
    return null;
  }
}

async function post<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${API_URL}/api/sites/${SITE_SLUG}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(body),
    cache: "no-store",
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    return { ok: false, error: (json as { error?: string }).error ?? `HTTP ${res.status}` } as T;
  }
  return json as T;
}

async function postForm<T>(path: string, body: FormData): Promise<T> {
  const res = await fetch(`${API_URL}/api/sites/${SITE_SLUG}${path}`, {
    method: "POST",
    headers: { Accept: "application/json" },
    body,
    cache: "no-store",
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    return { ok: false, error: (json as { error?: string }).error ?? `HTTP ${res.status}` } as T;
  }
  return json as T;
}

const HTML_ENTITIES: Record<string, string> = {
  "&nbsp;": " ",
  "&amp;": "&",
  "&lt;": "<",
  "&gt;": ">",
  "&quot;": "\"",
  "&apos;": "'",
  "&#39;": "'",
  "&ordf;": "ª",
  "&ordm;": "º",
  "&aacute;": "á", "&eacute;": "é", "&iacute;": "í", "&oacute;": "ó", "&uacute;": "ú",
  "&Aacute;": "Á", "&Eacute;": "É", "&Iacute;": "Í", "&Oacute;": "Ó", "&Uacute;": "Ú",
  "&atilde;": "ã", "&otilde;": "õ", "&ccedil;": "ç",
  "&Atilde;": "Ã", "&Otilde;": "Õ", "&Ccedil;": "Ç",
  "&acirc;": "â", "&ecirc;": "ê", "&ocirc;": "ô",
  "&agrave;": "à",
};

export function stripDuplicateCover(html: string, coverPath: string | null | undefined): string {
  if (!html || !coverPath) return html;
  const rx = /^\s*(?:<p[^>]*>\s*)?(?:<a[^>]*>\s*)?<img[^>]+src=(["'])([^"']+)\1[^>]*>\s*(?:<\/a>\s*)?(?:<\/p>)?/i;
  const m = html.match(rx);
  if (!m) return html;
  const src = m[2];
  const norm = (u: string) => u.replace(/^https?:\/\/[^/]+/, "").split("?")[0];
  if (norm(src) === norm(coverPath)) {
    return html.slice(m[0].length).trimStart();
  }
  return html;
}

export function cleanExcerpt(raw: string | null | undefined, maxLen = 220): string {
  if (!raw) return "";
  let s = raw;
  s = s.replace(/\{[^{}]*\}/g, "");
  s = s.replace(/<[^>]+>/g, " ");
  s = s.replace(/&#(\d+);/g, (_, code) => String.fromCharCode(Number(code)));
  s = s.replace(/&[a-zA-Z]+;/g, (m) => HTML_ENTITIES[m] ?? " ");
  s = s.replace(/\s+/g, " ").trim();
  if (s.length > maxLen) s = s.slice(0, maxLen).replace(/\s+\S*$/, "") + "…";
  return s;
}

export function formatBytes(bytes: number): string {
  if (!bytes || bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(1)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

export function formatDate(iso: string | null): string {
  if (!iso) return "";
  try {
    return new Date(iso).toLocaleDateString("pt-PT", {
      day: "2-digit",
      month: "long",
      year: "numeric",
    });
  } catch {
    return "";
  }
}
