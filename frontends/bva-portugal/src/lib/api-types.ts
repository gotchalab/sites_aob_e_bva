export type Site = {
  id: number;
  slug: string;
  name: string;
  domain: string;
  description: string | null;
  logoUrl: string | null;
  primaryColor: string | null;
  contactEmail: string | null;
  tagline: string | null;
  homeConfig: string | null;
  announcement: string | null;
};

export type HomeConfig = {
  mission?: string | null;
  missionTitle?: string | null;
  missionQuote?: string | null;
  missionImageUrl?: string | null;
  foundedYear?: number | null;
  memberCount?: number | null;
  ringsPerYear?: number | null;
  speciesCount?: number | null;
  areas?: HomeAreaItem[];
  benefits?: HomeBenefitItem[];
  ctaTitle?: string | null;
  ctaSubtitle?: string | null;
  ctaHref?: string | null;
  ctaLabel?: string | null;
};

export type HomeAreaItem = {
  icon: string;
  title: string;
  description: string;
  href?: string | null;
};

export type HomeBenefitItem = {
  icon: string;
  label: string;
  description: string;
};

export type Announcement = {
  enabled: boolean;
  message?: string | null;
  href?: string | null;
  tone?: "info" | "warning" | "event";
};

export type Sponsor = {
  id: number;
  name: string;
  slug: string;
  logoPath: string;
  clickUrl: string | null;
  tier: "Principal" | "Institucional" | "Apoio" | "Parceiro";
  sortOrder: number;
};

export type CategoryTree = {
  id: number;
  slug: string;
  name: string;
  description: string | null;
  sortOrder: number;
  children: CategoryTree[];
};

export type ArticleSummary = {
  id: number;
  slug: string;
  title: string;
  excerpt: string | null;
  coverImagePath: string | null;
  publishedAt: string | null;
  categorySlug: string;
  categoryName: string;
  isFeatured: boolean;
};

export type ArticleDetail = ArticleSummary & {
  content: string;
  metaTitle: string | null;
  metaDescription: string | null;
  viewCount: number;
  tags: string[];
};

export type DownloadSummary = {
  id: number;
  slug: string;
  title: string;
  description: string | null;
  fileName: string;
  fileSize: number;
  mimeType: string;
  publishedAt: string | null;
  downloadCount: number;
  categorySlug: string | null;
  categoryName: string | null;
};

export type DownloadDetail = DownloadSummary & {
  storagePath: string;
};

export type MenuItem = {
  id: number;
  title: string;
  url: string | null;
  targetType: "Internal" | "External" | "Article" | "Category" | "Download";
  targetId: number | null;
  sortOrder: number;
  openInNewTab: boolean;
  children: MenuItem[];
};

export type PagedResult<T> = {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
};

export type ContactRequest = {
  name: string;
  email: string;
  phone?: string;
  subject: string;
  message: string;
  turnstileToken?: string;
};

export type EstadoCivilOpt = "Solteiro" | "Casado" | "Divorciado" | "Viuvo";
export type StamStatus = "Nao" | "Sim" | "QueroTer";

export type InscricaoSocioRequest = {
  nomeCompleto: string;
  email: string;
  telefone?: string;
  cartaoCidadao?: string;
  nif?: string;
  nacionalidade?: string;
  dataNascimento?: string;
  estadoCivil?: EstadoCivilOpt | null;
  morada?: string;
  moradaLinha2?: string;
  codigoPostal?: string;
  localidade?: string;
  profissao?: string;
  socioApoiante: boolean;
  socioCriador: boolean;
  stamFonp?: StamStatus | null;
  stamFonpNumero?: string;
  socioBvaPortugal: boolean;
  stamBva?: StamStatus | null;
  stamBvaNumero?: string;
  aceitouRegulamento: boolean;
  notas?: string;
  turnstileToken?: string;
};

export type FormSubmissionResponse = {
  ok: boolean;
  error?: string | null;
  submissionId?: number | null;
  downloadToken?: string | null;
  tracesAvailable?: boolean;
  // HTTP status devolvido pelo servidor (429, 400, 500…). O cliente usa-o
  // para diferenciar rate-limit de erro de validação e mostrar UI adequada.
  status?: number;
  // Segundos até o rate-limit expirar (só definido em 429). Vem do header
  // Retry-After ou do corpo JSON do rate-limiter do backend.
  retryAfter?: number;
};

export type AveConvoyageDto = {
  serie: string;
  especieMutacao: string;
  especie: string;
  tipoClasse: string;
  anilha: string;
  equipaId?: string | null;
  posicaoEquipa?: "A" | "B" | "C" | "D" | null;
};

export type SexoAve = "Macho" | "Femea" | "Indefinido";

export type AveVendaDto = {
  especie: string;
  tipoClasse?: string;
  especieMutacao: string;
  especieLivre: boolean;
  dataNascimento: string; // yyyy-MM-dd
  sexo: SexoAve;
  preco: number;
  anilha: string;
};

export type OrigemAveTransporte = "Compra" | "Vende";

export type AveTransporteDto = {
  especie: string;
  origem: OrigemAveTransporte;
  anilha: string;
  destinatarioNome: string;
  destinatarioWhatsapp: string;
  destinatarioNotas?: string;
};

export type SocioBvaStatus = "JaSocio" | "PagaComInscricao" | "NaoSocio";

export type InscricaoConvoyageRequest = {
  nomeCompleto: string;
  email: string;
  telefone?: string;
  pais: string;
  numeroStam?: string;
  morada?: string;
  codigoPostal?: string;
  localidade?: string;
  assinaturaPngBase64?: string;
  declaraArt59?: boolean;
  localRecolhaId: number;
  aceitouRegulamento: boolean;
  socioBvaStatus: SocioBvaStatus;
  aves: AveConvoyageDto[];
  avesVenda?: AveVendaDto[];
  avesTransporte?: AveTransporteDto[];
  turnstileToken?: string;
};

export type ConvoyageCollectionPointDto = {
  id: number;
  name: string;
  location: string | null;
};

export type ConvoyageActiveYearDto = {
  id: number;
  year: number;
  description: string | null;
  collectionPoints: ConvoyageCollectionPointDto[];
  registrationClosesAt: string | null;
  // URL publica do regulamento (Download.StoragePath). Null se nao houver
  // regulamento associado ao ano — checkbox aparece sem link.
  regulamentoUrl: string | null;
  regulamentoFileName: string | null;
};

export type Species =
  | "Roseicollis" | "Personatus" | "Fischeri" | "Nigrigenis"
  | "Lilianae"    | "Canus"      | "Taranta"  | "Pullarius"
  | "Coelestis";

export type EntryType = "Individual" | "Team" | "Study";

export type NomenclatureClassDto = {
  id: number;
  code: string;
  mutation: string;
  notes: string | null;
};

export type NomenclatureGroupDto = {
  id: number;
  codePrefix: string;
  displayName: string;
  species: Species;
  entryType: EntryType;
  classes: NomenclatureClassDto[];
};

export type NomenclatureVersionDto = {
  convoyageYearId: number;
  year: number;
  groups: NomenclatureGroupDto[];
};
