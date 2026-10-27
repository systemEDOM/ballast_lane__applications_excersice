import { parseIsoDate, todayIso } from './dates';

export type LoginField = 'email' | 'password';
export type RegisterField = LoginField | 'displayName';
export type TaskField = 'title' | 'description' | 'dueDate';

export type FormErrors<Field extends string> = Partial<Record<Field, string>>;

const EMAIL_MAX = 254;
const NAME_MIN = 2;
const NAME_MAX = 80;
const PASSWORD_MIN = 8;
const PASSWORD_MAX = 128;
const TITLE_MIN = 3;
const TITLE_MAX = 120;
const DESCRIPTION_MAX = 1000;

export const PASSWORD_RULES = [
  { id: 'length', label: `At least ${PASSWORD_MIN} characters` },
  { id: 'letter', label: 'At least one letter' },
  { id: 'digit', label: 'At least one number' },
] as const;

export function checkPassword(value: string): Record<string, boolean> {
  return {
    length: value.length >= PASSWORD_MIN && value.length <= PASSWORD_MAX,
    letter: /[A-Za-z]/.test(value),
    digit: /\d/.test(value),
  };
}

export function validateEmail(value: string): string | undefined {
  const email = value.trim();
  if (email.length === 0) {
    return 'Enter your email address.';
  }
  if (email.length > EMAIL_MAX) {
    return `Email must be ${EMAIL_MAX} characters or fewer.`;
  }
  const parts = email.split('@');
  if (parts.length !== 2 || parts[0].length === 0 || parts[1].length === 0) {
    return 'Enter an email address with a single @.';
  }
  const domain = parts[1];
  if (!domain.includes('.') || domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) {
    return 'Enter a valid email domain, for example name@studio.com.';
  }
  return undefined;
}

export function validatePassword(value: string): string | undefined {
  if (value.length === 0) {
    return 'Enter your password.';
  }
  if (value.length < PASSWORD_MIN || value.length > PASSWORD_MAX) {
    return `Password must be between ${PASSWORD_MIN} and ${PASSWORD_MAX} characters.`;
  }
  if (!/[A-Za-z]/.test(value)) {
    return 'Password must include at least one letter.';
  }
  if (!/\d/.test(value)) {
    return 'Password must include at least one number.';
  }
  return undefined;
}

export function validateDisplayName(value: string): string | undefined {
  const name = value.trim();
  if (name.length < NAME_MIN || name.length > NAME_MAX) {
    return `Display name must be between ${NAME_MIN} and ${NAME_MAX} characters.`;
  }
  return undefined;
}

export function validateLogin(values: { email: string; password: string }): FormErrors<LoginField> {
  const errors: FormErrors<LoginField> = {};
  const email = validateEmail(values.email);
  if (email) {
    errors.email = email;
  }
  if (values.password.length === 0) {
    errors.password = 'Enter your password.';
  } else if (values.password !== values.password.trim()) {
    errors.password = 'Remove the space at the start or end of the password.';
  }
  return errors;
}

export function validateRegister(values: { email: string; displayName: string; password: string }): FormErrors<RegisterField> {
  const errors: FormErrors<RegisterField> = {};
  const email = validateEmail(values.email);
  if (email) {
    errors.email = email;
  }
  const displayName = validateDisplayName(values.displayName);
  if (displayName) {
    errors.displayName = displayName;
  }
  const password = validatePassword(values.password);
  if (password) {
    errors.password = password;
  }
  return errors;
}

export interface TaskFormValues {
  title: string;
  description: string;
  dueDate: string;
}

export function validateTask(values: TaskFormValues): FormErrors<TaskField> {
  const errors: FormErrors<TaskField> = {};
  const title = values.title.trim();
  if (title.length < TITLE_MIN || title.length > TITLE_MAX) {
    errors.title = `Title must be between ${TITLE_MIN} and ${TITLE_MAX} characters.`;
  }

  if (values.description.length > DESCRIPTION_MAX) {
    errors.description = `Description must be ${DESCRIPTION_MAX} characters or fewer.`;
  }

  if (values.dueDate.length > 0) {
    const date = parseIsoDate(values.dueDate);
    if (!date) {
      errors.dueDate = 'Enter a valid date.';
    } else if (values.dueDate < todayIso()) {
      errors.dueDate = 'Due date cannot be in the past.';
    }
  }

  return errors;
}

export function mapFieldErrors<Field extends string>(
  fieldErrors: Record<string, string[]>,
  fields: readonly Field[],
): FormErrors<Field> {
  const mapped: FormErrors<Field> = {};
  for (const field of fields) {
    const messages = fieldErrors[field];
    if (messages && messages.length > 0 && messages[0]) {
      mapped[field] = messages[0];
    }
  }
  return mapped;
}
