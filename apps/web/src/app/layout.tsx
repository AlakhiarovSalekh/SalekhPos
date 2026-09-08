import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = { title: "SalekhPos — Retail workspace", description: "Your SalekhPos retail workspace." };

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
