import { ApiClient } from "@salekhpos/packages-api-client";
import { createContext, useContext, useMemo, type PropsWithChildren } from "react";

const ApiContext = createContext<ApiClient | null>(null);

export function ApiProvider({
  baseUrl,
  accessToken,
  children,
}: PropsWithChildren<Readonly<{ baseUrl: string; accessToken: string | null }>>) {
  const client = useMemo(
    () => new ApiClient({
      baseUrl,
      tokenProvider: { async getAccessToken() { return accessToken; } },
      maxResponseBytes: 512 * 1024,
    }),
    [accessToken, baseUrl],
  );
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>;
}

export function useApiClient(): ApiClient {
  const client = useContext(ApiContext);
  if (client === null) throw new Error("useApiClient must be used within ApiProvider.");
  return client;
}
