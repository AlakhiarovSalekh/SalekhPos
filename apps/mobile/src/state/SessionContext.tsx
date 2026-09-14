import {
  AppState,
  type AppStateStatus,
} from "react-native";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type PropsWithChildren,
} from "react";

import {
  getSessionValidity,
  InvalidSessionError,
  parseSession,
  type AuthenticatedSession,
} from "@/security/session";
import { secureSessionStore, type SessionStore } from "@/storage/sessionStore";

export type SessionStatus = "restoring" | "signedOut" | "authenticated" | "error";

type SessionContextValue = Readonly<{
  status: SessionStatus;
  session: AuthenticatedSession | null;
  restoreError: Error | null;
  restore: () => Promise<void>;
  establishSession: (session: AuthenticatedSession) => Promise<void>;
  signOut: () => Promise<void>;
}>;

const SessionContext = createContext<SessionContextValue | null>(null);

export function SessionProvider({
  children,
  store = secureSessionStore,
  now = Date.now,
}: PropsWithChildren<{
  store?: SessionStore;
  now?: () => number;
}>) {
  const [status, setStatus] = useState<SessionStatus>("restoring");
  const [session, setSession] = useState<AuthenticatedSession | null>(null);
  const [restoreError, setRestoreError] = useState<Error | null>(null);
  const restoreInFlight = useRef<Promise<void> | null>(null);

  const restore = useCallback(() => {
    if (restoreInFlight.current !== null) {
      return restoreInFlight.current;
    }

    const operation = (async () => {
      setStatus("restoring");
      setRestoreError(null);
      try {
        const restored = await store.load();
        if (restored === null) {
          setSession(null);
          setStatus("signedOut");
          return;
        }
        if (getSessionValidity(restored, now()) === "expired") {
          await store.clear();
          setSession(null);
          setStatus("signedOut");
          return;
        }
        setSession(restored);
        setStatus("authenticated");
      } catch (error) {
        if (error instanceof InvalidSessionError) {
          try {
            await store.clear();
            setSession(null);
            setStatus("signedOut");
            return;
          } catch (clearError) {
            error = clearError;
          }
        }
        setSession(null);
        setRestoreError(error instanceof Error ? error : new Error("Session restore failed."));
        setStatus("error");
      }
    })().finally(() => {
      restoreInFlight.current = null;
    });

    restoreInFlight.current = operation;
    return operation;
  }, [now, store]);

  useEffect(() => {
    void restore();
  }, [restore]);

  useEffect(() => {
    const onAppStateChange = (nextState: AppStateStatus) => {
      if (nextState === "active") {
        void restore();
      }
    };
    const subscription = AppState.addEventListener("change", onAppStateChange);
    return () => subscription.remove();
  }, [restore]);

  const establishSession = useCallback(
    async (candidate: AuthenticatedSession) => {
      const validSession = parseSession(candidate);
      if (getSessionValidity(validSession, now()) === "expired") {
        throw new Error("Cannot establish an expired session.");
      }
      await store.save(validSession);
      setRestoreError(null);
      setSession(validSession);
      setStatus("authenticated");
    },
    [now, store],
  );

  const signOut = useCallback(async () => {
    try {
      await store.clear();
      setRestoreError(null);
      setSession(null);
      setStatus("signedOut");
    } catch (error) {
      setRestoreError(error instanceof Error ? error : new Error("Secure sign out failed."));
      setStatus("error");
    }
  }, [store]);

  const value = useMemo(
    () => ({ status, session, restoreError, restore, establishSession, signOut }),
    [status, session, restoreError, restore, establishSession, signOut],
  );

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionContextValue {
  const value = useContext(SessionContext);
  if (value === null) {
    throw new Error("useSession must be used within SessionProvider.");
  }
  return value;
}
