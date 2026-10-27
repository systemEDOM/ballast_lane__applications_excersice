import type { TaskStatus } from '../api/types';

const DAY_MS = 86_400_000;

const shortDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' });
const longDate = new Intl.DateTimeFormat('en-GB', {
  weekday: 'short',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function todayIso(): string {
  return toIsoDate(new Date());
}

export function parseIsoDate(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }
  const parts = value.split('-');
  const year = Number(parts[0]);
  const month = Number(parts[1]);
  const day = Number(parts[2]);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}

export function daysFromToday(value: string): number | null {
  const date = parseIsoDate(value);
  const today = parseIsoDate(todayIso());
  if (!date || !today) {
    return null;
  }
  return Math.round((date.getTime() - today.getTime()) / DAY_MS);
}

export type DueTone = 'overdue' | 'today' | 'soon' | 'normal' | 'done';

export interface DueSummary {
  tone: DueTone;
  label: string;
  full: string;
}

function pluralDays(count: number): string {
  return count === 1 ? '1 day' : `${count} days`;
}

export function formatShortDate(value: string): string {
  const date = parseIsoDate(value);
  return date ? shortDate.format(date) : value;
}

export function formatLongDate(value: string): string {
  const date = parseIsoDate(value);
  return date ? longDate.format(date) : value;
}

export function dueSummary(dueDate: string | null, status: TaskStatus): DueSummary | null {
  if (!dueDate) {
    return null;
  }

  const date = parseIsoDate(dueDate);
  if (!date) {
    return null;
  }

  const full = longDate.format(date);
  const short = shortDate.format(date);

  if (status === 'Done') {
    return { tone: 'done', label: `Due ${short}`, full };
  }

  const remaining = daysFromToday(dueDate);
  if (remaining === null) {
    return { tone: 'normal', label: `Due ${short}`, full };
  }

  if (remaining < 0) {
    return { tone: 'overdue', label: `${pluralDays(-remaining)} overdue`, full };
  }
  if (remaining === 0) {
    return { tone: 'today', label: 'Due today', full };
  }
  if (remaining === 1) {
    return { tone: 'soon', label: 'Due tomorrow', full };
  }
  if (remaining <= 3) {
    return { tone: 'soon', label: `Due in ${remaining} days`, full };
  }
  return { tone: 'normal', label: `Due ${short}`, full };
}

export function isOverdue(dueDate: string | null, status: TaskStatus): boolean {
  return dueSummary(dueDate, status)?.tone === 'overdue';
}
