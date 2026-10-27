import { request } from './client';
import type { Task, TaskInput, TaskStatus } from './types';

export function listTasks(signal?: AbortSignal): Promise<Task[]> {
  return request<Task[]>('/tasks', { signal });
}

export function createTask(input: TaskInput): Promise<Task> {
  return request<Task>('/tasks', { method: 'POST', body: input });
}

export function updateTask(id: string, input: TaskInput): Promise<Task> {
  return request<Task>(`/tasks/${id}`, { method: 'PUT', body: input });
}

export function changeTaskStatus(id: string, status: TaskStatus): Promise<Task> {
  return request<Task>(`/tasks/${id}/status`, { method: 'PATCH', body: { status } });
}

export function deleteTask(id: string): Promise<void> {
  return request<void>(`/tasks/${id}`, { method: 'DELETE' });
}
