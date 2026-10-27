import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError, setUnauthorizedHandler } from '../api/client';
import { getCurrentUser, login as loginRequest, register as registerRequest } from '../api/auth';
import type { AuthResult, User } from '../api/types';
import { useToast } from '../components/ToastProvider';
import { AuthContext, type AuthStatus, type AuthContextValue } from './context';
import { clearSession, readSession, writeSession } from './session';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [initialSession] = useState(() => readSession());
  const [user, setUser] = useState<User | null>(initialSession?.user ?? null);
  const [status, setStatus] = useState<AuthStatus>(initialSession ? 'checking' : 'anonymous');
  const bootstrapping = useRef(initialSession !== null);
  const queryClient = useQueryClient();
  const { notify } = useToast();

  useEffect(() => {
    if (!initialSession) {
      return;
    }

    const controller = new AbortController();
    let active = true;

    getCurrentUser(controller.signal)
      .then((freshUser) => {
        if (!active) {
          return;
        }
        writeSession(initialSession.token, freshUser);
        setUser(freshUser);
        setStatus('authenticated');
      })
      .catch((error: unknown) => {
        if (!active) {
          return;
        }
        if (error instanceof DOMException && error.name === 'AbortError') {
          return;
        }
        if (error instanceof ApiError && error.status === 401) {
          clearSession();
          setUser(null);
          setStatus('anonymous');
          return;
        }
        // Server unreachable or unavailable: keep the cached session so the board
        // can show its own error state instead of dropping the user at the door.
        setStatus('authenticated');
      })
      .finally(() => {
        if (active) {
          bootstrapping.current = false;
        }
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [initialSession]);

  useEffect(() => {
    const handleUnauthorized = () => {
      clearSession();
      setUser(null);
      setStatus('anonymous');
      queryClient.clear();
      if (!bootstrapping.current) {
        notify({ tone: 'error', message: 'Your session ended. Sign in again to continue.' });
      }
    };

    setUnauthorizedHandler(handleUnauthorized);
    return () => setUnauthorizedHandler(null);
  }, [notify, queryClient]);

  const adopt = useCallback(
    (result: AuthResult) => {
      writeSession(result.accessToken, result.user);
      setUser(result.user);
      setStatus('authenticated');
      queryClient.clear();
    },
    [queryClient],
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const result = await loginRequest({ email, password });
      adopt(result);
    },
    [adopt],
  );

  const register = useCallback(
    async (input: { email: string; displayName: string; password: string }) => {
      const result = await registerRequest(input);
      adopt(result);
    },
    [adopt],
  );

  const logout = useCallback(() => {
    clearSession();
    setUser(null);
    setStatus('anonymous');
    queryClient.clear();
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, register, logout }),
    [status, user, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
