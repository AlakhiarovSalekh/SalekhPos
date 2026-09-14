import * as SecureStore from "expo-secure-store";

import {
  deserializeSession,
  serializeSession,
  type AuthenticatedSession,
} from "@/security/session";

const sessionStorageKey = "com.salekhpos.mobile.session.v1";

export type SessionStore = Readonly<{
  load: () => Promise<AuthenticatedSession | null>;
  save: (session: AuthenticatedSession) => Promise<void>;
  clear: () => Promise<void>;
}>;

export const secureSessionStore: SessionStore = Object.freeze({
  async load() {
    const stored = await SecureStore.getItemAsync(sessionStorageKey);
    return stored === null ? null : deserializeSession(stored);
  },
  async save(session) {
    await SecureStore.setItemAsync(sessionStorageKey, serializeSession(session), {
      keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
    });
  },
  async clear() {
    await SecureStore.deleteItemAsync(sessionStorageKey);
  },
});
