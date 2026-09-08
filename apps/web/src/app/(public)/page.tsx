import Link from "next/link";

export default function Page() {
  return <main className="landing"><nav><Link className="brand" href="/">Salekh<span>Pos</span><span className="brand-dot" /></Link><Link className="text-link" href="/sign-in">Open workspace ↗</Link></nav><section className="hero"><p className="eyebrow">THE RETAIL WORKSPACE</p><h1>One place.<br />Every store.</h1><p className="hero-copy">A shared workspace for the people behind your business.</p><Link className="button" href="/sign-in">Sign in to your workspace <span aria-hidden="true">→</span></Link><p className="quiet">SalekhPos is in active development.</p></section><aside className="hero-panel"><div className="panel-mark" aria-hidden="true">S</div><p>Built around<br /><strong>your business.</strong></p><div className="panel-footer">SALEKHPOS <span>RETAIL OPERATIONS</span></div></aside></main>;
}
