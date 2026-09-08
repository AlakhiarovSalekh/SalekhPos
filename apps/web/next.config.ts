import type { NextConfig } from "next";

const backend = process.env.SALEKHPOS_BACKEND_ORIGIN ?? "http://127.0.0.1:5080";
const url = new URL(backend);
if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.pathname !== "/" || url.search || url.hash) throw new Error("Invalid backend origin");
const config: NextConfig = {
  poweredByHeader: false,
  async rewrites() { return [{ source: "/auth/:path*", destination: `${url.origin}/auth/:path*` }]; },
  async headers() { return [{ source: "/:path*", headers: [
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "X-Frame-Options", value: "DENY" },
    { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
    { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" }
  ] }]; }
};
export default config;
