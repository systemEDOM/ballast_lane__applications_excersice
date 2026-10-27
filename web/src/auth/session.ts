import type { User } from '../api/types';

const TOKEN_KEY = 'bl.token';
const USER_KEY = 'bl.user';

export interface StoredSession {
  token: string;
  user: User;
}

function isUser(value: unknown): value is User {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.id === 'string' &&
    typeof candidate.email === 'string' &&
    typeof candidate.displayName === 'string' &&
    typeof candidate.createdAt === 'string'
  );
}

export function readToken(): string | null {
  try {
    return window.localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function readSession(): StoredSession | null {
  try {
    const token = window.localStorage.getItem(TOKEN_KEY);
    const rawUser = window.localStorage.getItem(USER_KEY);
    if (!token || !rawUser) {
      return null;
    }
    const parsed: unknown = JSON.parse(rawUser);
    if (!isUser(parsed)) {
      return null;
    }
    return { token, user: parsed };
  } catch {
    return null;
  }
}

export function writeSession(token: string, user: User): void {
  try {
    window.localStorage.setItem(TOKEN_KEY, token);
    window.localStorage.setItem(USER_KEY, JSON.stringify(user));
  } catch {
    // Storage can be unavailable in private modes; the session then lives in memory only.
  }
}

export function clearSession(): void {
  try {
    window.localStorage.removeItem(TOKEN_KEY);
    window.localStorage.removeItem(USER_KEY);
  } catch {
    // Nothing to clean up when storage is blocked.
  }
}
