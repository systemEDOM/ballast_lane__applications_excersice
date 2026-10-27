import type { TaskStatus } from '../api/types';

export const STATUS_ORDER: TaskStatus[] = ['Todo', 'InProgress', 'Done'];

export const STATUS_LABEL: Record<TaskStatus, string> = {
  Todo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
};

export const STATUS_CLASS: Record<TaskStatus, string> = {
  Todo: 'todo',
  InProgress: 'in-progress',
  Done: 'done',
};

export interface StatusAdvance {
  status: TaskStatus;
  label: string;
}

export function nextStatus(status: TaskStatus): StatusAdvance | null {
  if (status === 'Todo') {
    return { status: 'InProgress', label: 'Start' };
  }
  if (status === 'InProgress') {
    return { status: 'Done', label: 'Complete' };
  }
  return null;
}

export function isTaskStatus(value: unknown): value is TaskStatus {
  return value === 'Todo' || value === 'InProgress' || value === 'Done';
}
