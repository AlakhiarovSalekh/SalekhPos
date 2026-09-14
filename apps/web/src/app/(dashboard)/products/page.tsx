import Link from "next/link";
import { ProductManager } from "@/features/products/ProductManager";
import "./products.css";

export default function ProductsPage() {
  return <main className="workspace products-workspace"><nav><Link className="brand" href="/">Salekh<span>Pos</span><span className="brand-dot" /></Link><span className="quiet">PRODUCT MANAGEMENT</span></nav><div className="products-content"><ProductManager /></div></main>;
}
