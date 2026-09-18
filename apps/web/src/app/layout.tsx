import type { Metadata } from "next";
import "./globals.css";
import "./inventory.css";
import "./manager.css";
import { ThemeToggle } from "@/components/theme/ThemeToggle";

export const metadata: Metadata = { title: "SalekhPos — Retail workspace", description: "Your SalekhPos retail workspace." };

const themeBootstrap = `(()=>{try{const stored=localStorage.getItem("salekhpos-theme");const theme=stored==="light"||stored==="dark"?stored:(matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light");document.documentElement.dataset.theme=theme}catch{}})();`;

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en" suppressHydrationWarning>
    <head><script dangerouslySetInnerHTML={{ __html: themeBootstrap }} /></head>
    <body>{children}<ThemeToggle /></body>
  </html>;
}