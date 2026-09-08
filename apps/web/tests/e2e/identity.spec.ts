import { expect, test } from "@playwright/test";

test("real provider code flow creates a server session and logout invalidates a copied cookie", async ({ page, browser }) => {
  const password = process.env.SALEKHPOS_BROWSER_PASSWORD;
  if (!password) throw new Error("Use scripts/test-postgres.ps1 -RunWebIdentityTests; live identity tests never silently skip.");
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto("/sign-in");
  const [loginRequest] = await Promise.all([
    page.waitForRequest(request => request.url().endsWith("/auth/login")),
    page.getByRole("button", { name: "Continue to sign in" }).click()
  ]);
  expect(loginRequest.headers().origin).toBe("http://localhost:3000");
  await expect(page).toHaveURL(/127\.0\.0\.1:8180/);
  await page.locator('input[name="username"]').fill("browser-user");
  await page.locator('input[name="password"]').fill(password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await expect(page).toHaveURL("http://localhost:3000/dashboard");
  await expect(page.getByRole("heading", { name: "Welcome back." })).toBeVisible();
  await expect(page.getByText("Store Operator", { exact: true })).toBeVisible();
  const cookies = await page.context().cookies();
  const replay = await browser.newContext();
  await replay.addCookies(cookies);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL("http://localhost:3000/sign-in");
  await expect(page.getByRole("button", { name: "Continue to sign in" })).toBeVisible();
  const replayPage = await replay.newPage();
  await replayPage.goto("http://localhost:3000/dashboard");
  await expect(replayPage.getByRole("button", { name: "Continue to sign in" })).toBeVisible();
  expect(errors).toEqual([]);
  await replay.close();
});
