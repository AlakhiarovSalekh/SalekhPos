import Link from "next/link";
import { SessionPanel } from "@/features/auth/SessionPanel";

export default async function SignInPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const { error } = await searchParams;
  return <main className="auth-shell"><aside className="auth-story"><Link className="brand" href="/">Salekh<span>Pos</span><span className="brand-dot" /></Link><div><p className="eyebrow">READY FOR YOUR NEXT CHAPTER</p><h2>Behind every<br />store,<br /><em>there’s you.</em></h2></div><p className="story-foot">YOUR PEOPLE. YOUR STORES. ONE WORKSPACE.</p></aside><section className="auth-content"><Link href="/" className="back-link">← Back to home</Link><div className="auth-card">{error && <p role="alert" className="error">Sign-in was not completed. Please try again.</p>}<SessionPanel /></div><footer>SalekhPos <span>Retail workspace</span></footer></section></main>;
}
