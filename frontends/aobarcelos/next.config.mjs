/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // output:"standalone" usa symlinks → EPERM no Windows sem Developer Mode
  // Deploy via dist/ gerado por npm run build:prod (ver scripts/build-prod.mjs)
  webpack(config, { dev }) {
    // Impede eval() nos bundles de produção (Edge Runtime não suporta eval)
    if (!dev) config.devtool = false;
    return config;
  },
  images: {
    remotePatterns: [
      { protocol: "http", hostname: "localhost", port: "5135", pathname: "/uploads/**" },
      { protocol: "https", hostname: "aobarcelos.pt", pathname: "/uploads/**" },
      { protocol: "https", hostname: "bva-p.aobarcelos.pt", pathname: "/uploads/**" },
      { protocol: "https", hostname: "api.aobarcelos.pt", pathname: "/uploads/**" },
    ],
  },
  async rewrites() {
    if (process.env.NODE_ENV === "production") return [];
    return [
      { source: "/uploads/:path*", destination: `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5135"}/uploads/:path*` },
    ];
  },
};

export default nextConfig;
