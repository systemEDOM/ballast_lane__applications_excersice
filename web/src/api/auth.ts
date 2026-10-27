import { request } from './client';
import type { AuthResult, User } from './types';

export interface LoginInput {
  email: string;
  password: string;
}

export interface RegisterInput {
  email: string;
  displayName: string;
  password: string;
}

export function login(input: LoginInput): Promise<AuthResult> {
  return request<AuthResult>('/auth/login', { method: 'POST', body: input, auth: false });
}

export function register(input: RegisterInput): Promise<AuthResult> {
  return request<AuthResult>('/auth/register', { method: 'POST', body: input, auth: false });
}

export function getCurrentUser(signal?: AbortSignal): Promise<User> {
  return request<User>('/auth/me', { signal });
}
