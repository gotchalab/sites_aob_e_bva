export const API_URL =
  typeof window === "undefined"
    ? (process.env.API_INTERNAL_URL ?? process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000")
    : (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000");

export const SITE_SLUG = process.env.NEXT_PUBLIC_SITE_SLUG ?? "bva";
