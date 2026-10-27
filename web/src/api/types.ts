export type TaskStatus = 'Todo' | 'InProgress' | 'Done';

export interface Task {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  dueDate: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
}

export interface AuthResult {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface TaskInput {
  title: string;
  description: string | null;
  dueDate: string | null;
}
