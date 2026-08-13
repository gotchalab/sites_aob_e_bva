/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // output: "standalone" — desativado: cria symlinks no Windows (EPERM)
  // Deploy via .next/ + npm ci --production no VPS
  images: {
    remotePatterns: [
      { protocol: "http", hostname: "localhost", port: "5000", pathname: "/uploads/**" },
      { protocol: "https", hostname: "aobarcelos.pt", pathname: "/uploads/**" },
      { protocol: "https", hostname: "bva-p.aobarcelos.pt", pathname: "/uploads/**" },
      { protocol: "https", hostname: "api.aobarcelos.pt", pathname: "/uploads/**" },
    ],
  },
  async rewrites() {
    if (process.env.NODE_ENV === "production") return [];
    return [
      { source: "/uploads/:path*", destination: `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/uploads/:path*` },
    ];
  },
};

export default nextConfig;
