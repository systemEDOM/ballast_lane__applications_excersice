import { clearSession, readToken } from '../auth/session';

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const API_BASE_URL = rawBaseUrl.replace(/\/+$/, '');

interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export interface ApiErrorOptions {
  code?: string | null;
  fieldErrors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, message: string, options: ApiErrorOptions = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = options.code ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

type UnauthorizedHandler = () => void;

let unauthorizedHandler: UnauthorizedHandler | null = null;

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler;
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  auth?: boolean;
  signal?: AbortSignal;
}

const fallbackMessages: Record<number, string> = {
  400: 'Some details need another look.',
  401: 'Your session has ended. Sign in again to continue.',
  403: 'You do not have access to that.',
  404: 'We could not find that item.',
  409: 'That already exists.',
  500: 'Something went wrong on our side. Try again.',
};

function normalizeFieldName(key: string): string {
  const cleaned = key.replace(/[\s_-]/g, '');
  if (cleaned.length === 0) {
    return key;
  }
  return cleaned.charAt(0).toLowerCase() + cleaned.slice(1);
}

function normalizeFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  if (!errors) {
    return {};
  }

  const normalized: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(errors)) {
    if (!Array.isArray(messages)) {
      continue;
    }
    const values = messages.filter((message): message is string => typeof message === 'string' && message.length > 0);
    if (values.length > 0) {
      normalized[normalizeFieldName(key)] = values;
    }
  }
  return normalized;
}

function problemMessage(status: number, problem: ProblemDetails | null, fieldErrors: Record<string, string[]>): string {
  const firstFieldMessage = Object.values(fieldErrors)[0]?.[0];
  if (firstFieldMessage) {
    return firstFieldMessage;
  }
  if (problem?.title && problem.title.trim().length > 0) {
    return problem.title;
  }
  if (problem?.detail && problem.detail.trim().length > 0) {
    return problem.detail;
  }
  return fallbackMessages[status] ?? 'Something went wrong. Try again.';
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, auth = true, signal } = options;

  const headers = new Headers({ Accept: 'application/json' });
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json');
  }

  const token = auth ? readToken() : null;
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      credentials: 'omit',
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }
    throw new ApiError(0, 'We could not reach the server. Check your connection and try again.', {
      code: 'network_unreachable',
    });
  }

  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('json')) {
      try {
        problem = (await response.json()) as ProblemDetails;
      } catch {
        problem = null;
      }
    }

    const fieldErrors = normalizeFieldErrors(problem?.errors);

    if (response.status === 401 && auth) {
      clearSession();
      unauthorizedHandler?.();
    }

    throw new ApiError(response.status, problemMessage(response.status, problem, fieldErrors), {
      code: problem?.code ?? null,
      fieldErrors,
    });
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('json')) {
    return (await response.text()) as unknown as T;
  }

  return (await response.json()) as T;
}
