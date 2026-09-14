"use client";

import Link from "next/link";
import { type FormEvent, useEffect, useRef, useState } from "react";
import {
  createProduct, getProductCreateIntent, listOrganizations, listProducts, loadCsrfToken,
  productErrorMessage, validateProductDraft,
  type AccessibleOrganization, type Product, type ProductCreateIntent, type ProductDraft,
} from "./api";

const emptyDraft: ProductDraft = { sku: "", name: "", unitCode: "EA", barcode: "" };

export function ProductManager() {
  const [organizations, setOrganizations] = useState<readonly AccessibleOrganization[]>([]);
  const [organizationCursor, setOrganizationCursor] = useState<string | null>(null);
  const [selectedOrganization, setSelectedOrganization] = useState("");
  const [csrfToken, setCsrfToken] = useState("");
  const [products, setProducts] = useState<readonly Product[]>([]);
  const [productCursor, setProductCursor] = useState<string | null>(null);
  const [productAfter, setProductAfter] = useState<string | null>(null);
  const [productHistory, setProductHistory] = useState<readonly (string | null)[]>([]);
  const [productReload, setProductReload] = useState(0);
  const [draft, setDraft] = useState<ProductDraft>(emptyDraft);
  const [loadingOrganizations, setLoadingOrganizations] = useState(true);
  const [loadingProducts, setLoadingProducts] = useState(false);
  const [saving, setSaving] = useState(false);
  const [pageError, setPageError] = useState("");
  const [productError, setProductError] = useState("");
  const [formError, setFormError] = useState("");
  const [notice, setNotice] = useState("");
  const createController = useRef<AbortController | null>(null);
  const createIntent = useRef<ProductCreateIntent | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([loadCsrfToken(controller.signal), listOrganizations(null, controller.signal)])
      .then(([token, page]) => {
        setCsrfToken(token);
        setOrganizations(page.items);
        setOrganizationCursor(page.nextCursor);
        if (page.items.length > 0) setLoadingProducts(true);
        setSelectedOrganization(page.items[0]?.id ?? "");
      })
      .catch((error: unknown) => { if (!controller.signal.aborted) setPageError(productErrorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setLoadingOrganizations(false); });
    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (!selectedOrganization) return;
    const controller = new AbortController();
    listProducts(selectedOrganization, productAfter, controller.signal)
      .then((page) => { setProducts(page.items); setProductCursor(page.nextCursor); })
      .catch((error: unknown) => { if (!controller.signal.aborted) setProductError(productErrorMessage(error)); })
      .finally(() => { if (!controller.signal.aborted) setLoadingProducts(false); });
    return () => controller.abort();
  }, [selectedOrganization, productAfter, productReload]);

  useEffect(() => () => createController.current?.abort(), []);

  async function loadMoreOrganizations() {
    if (!organizationCursor || loadingOrganizations) return;
    setLoadingOrganizations(true); setPageError("");
    const controller = new AbortController();
    try {
      const page = await listOrganizations(organizationCursor, controller.signal);
      const known = new Set(organizations.map((organization) => organization.id));
      if (page.items.some((organization) => known.has(organization.id))) throw new Error("Malformed organization response");
      setOrganizations((current) => [...current, ...page.items]);
      setOrganizationCursor(page.nextCursor);
    } catch (error: unknown) { setPageError(productErrorMessage(error)); }
    finally { setLoadingOrganizations(false); }
  }

  function chooseOrganization(id: string) {
    setLoadingProducts(true); setProductError(""); setSelectedOrganization(id);
    setProductAfter(null); setProductHistory([]); setNotice(""); createIntent.current = null;
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const validationError = validateProductDraft(draft);
    if (validationError) { setFormError(validationError); return; }
    createController.current?.abort();
    const controller = new AbortController();
    createController.current = controller;
    setSaving(true); setFormError(""); setNotice("");
    try {
      const intent = getProductCreateIntent(selectedOrganization, draft, createIntent.current);
      createIntent.current = intent;
      const created = await createProduct(selectedOrganization, draft, csrfToken, intent.idempotencyKey, controller.signal);
      createIntent.current = null;
      setDraft(emptyDraft); setLoadingProducts(true); setProductAfter(null); setProductHistory([]);
      setProductReload((version) => version + 1);
      setNotice(`${created.name} was added to the catalog.`);
    } catch (error: unknown) { if (!controller.signal.aborted) setFormError(productErrorMessage(error)); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }

  if (loadingOrganizations && organizations.length === 0) return <p className="quiet" role="status">Loading your organizations…</p>;
  if (pageError && organizations.length === 0) return <div className="notice error" role="alert"><h2>Products are unavailable</h2><p>{pageError}</p><Link className="text-link" href="/dashboard">Back to dashboard</Link></div>;
  if (organizations.length === 0) return <div className="notice" role="status"><h2>No organization access</h2><p>Your account does not currently have an active organization membership.</p><Link className="text-link" href="/dashboard">Back to dashboard</Link></div>;

  return <>
    <header className="products-heading"><div><p className="eyebrow">CATALOG</p><h1>Products<span className="accent">.</span></h1><p className="description">Keep the essentials your team uses at checkout clear and current.</p></div><Link className="text-link" href="/dashboard">← Dashboard</Link></header>
    <section className="catalog-controls" aria-labelledby="organization-label"><label id="organization-label" htmlFor="organization">Organization</label><select id="organization" value={selectedOrganization} onChange={(event) => chooseOrganization(event.target.value)}>{organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}</select>{organizationCursor && <button className="text-button" type="button" disabled={loadingOrganizations} onClick={loadMoreOrganizations}>Load more organizations</button>}{pageError && <p className="field-error" role="alert">{pageError}</p>}</section>
    <div className="catalog-grid">
      <section className="product-list" aria-labelledby="product-list-title" aria-busy={loadingProducts}><div className="section-title"><div><p className="eyebrow">IN THIS ORGANIZATION</p><h2 id="product-list-title">Product list</h2></div><span className="quiet">{products.length} on this page</span></div>{loadingProducts && <p className="quiet" role="status">Loading products…</p>}{!loadingProducts && productError && <div className="error" role="alert">{productError}</div>}{!loadingProducts && !productError && products.length === 0 && <div className="empty-state" role="status"><h3>No products yet</h3><p>Add the first product using the form.</p></div>}{!loadingProducts && !productError && products.length > 0 && <div className="table-wrap"><table><thead><tr><th scope="col">Product</th><th scope="col">SKU</th><th scope="col">Unit</th><th scope="col">Barcode</th></tr></thead><tbody>{products.map((product) => <tr key={product.id}><td>{product.name}{!product.isActive && <span className="badge">Inactive</span>}</td><td><code>{product.sku}</code></td><td>{product.unitCode}</td><td>{product.barcode ?? "—"}</td></tr>)}</tbody></table></div>}<div className="pagination" aria-label="Product pages"><button type="button" className="secondary-button" disabled={loadingProducts || productHistory.length === 0} onClick={() => { setLoadingProducts(true); setProductError(""); setProductAfter(productHistory.at(-1) ?? null); setProductHistory((history) => history.slice(0, -1)); }}>Previous</button><button type="button" className="secondary-button" disabled={loadingProducts || productCursor === null} onClick={() => { if (productCursor) { setLoadingProducts(true); setProductError(""); setProductHistory((history) => [...history, productAfter]); setProductAfter(productCursor); } }}>Next</button></div></section>
      <section className="product-form-card" aria-labelledby="add-product-title"><p className="eyebrow">NEW ITEM</p><h2 id="add-product-title">Add a product</h2><form onSubmit={submit} noValidate><label htmlFor="sku">SKU</label><input id="sku" name="sku" required maxLength={64} autoComplete="off" value={draft.sku} onChange={(event) => setDraft({ ...draft, sku: event.target.value.toUpperCase() })} /><label htmlFor="product-name">Name</label><input id="product-name" name="name" required maxLength={200} autoComplete="off" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value })} /><label htmlFor="unit-code">Unit</label><input id="unit-code" name="unitCode" required maxLength={16} autoComplete="off" value={draft.unitCode} onChange={(event) => setDraft({ ...draft, unitCode: event.target.value.toUpperCase() })} /><label htmlFor="barcode">Barcode <span className="quiet">optional</span></label><input id="barcode" name="barcode" inputMode="numeric" maxLength={64} autoComplete="off" value={draft.barcode} onChange={(event) => setDraft({ ...draft, barcode: event.target.value })} />{formError && <p className="field-error" role="alert">{formError}</p>}{notice && <p className="success" role="status">{notice}</p>}<button className="button" type="submit" disabled={saving || !selectedOrganization}>{saving ? "Adding product…" : "Add product"}</button></form></section>
    </div>
  </>;
}
