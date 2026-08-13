// Server-side (SSR/ISR/build): usa URL interna — não fica no bundle do browser
// Client-side: usa NEXT_PUBLIC_API_URL — baked into bundle durante o build
export const API_URL =
  typeof window === "undefined"
    ? (process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000")
    : (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000");

export const SITE_SLUG = process.env.NEXT_PUBLIC_SITE_SLUG ?? "aob";
