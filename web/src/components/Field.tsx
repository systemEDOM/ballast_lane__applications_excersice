import type { ReactNode } from 'react';

interface FieldProps {
  id: string;
  label: string;
  error?: string;
  hint?: string;
  optional?: boolean;
  counter?: string;
  children: ReactNode;
}

export function Field({ id, label, error, hint, optional, counter, children }: FieldProps) {
  return (
    <div className={error ? 'field field--invalid' : 'field'}>
      <div className="field__head">
        <label className="field__label" htmlFor={id}>
          {label}
          {optional ? <span className="field__optional">Optional</span> : null}
        </label>
        {counter ? <span className="field__counter">{counter}</span> : null}
      </div>
      {children}
      {error ? (
        <p className="field__error" id={`${id}-error`}>
          {error}
        </p>
      ) : hint ? (
        <p className="field__hint" id={`${id}-hint`}>
          {hint}
        </p>
      ) : null}
    </div>
  );
}

export function describedBy(id: string, error?: string, hint?: string): string | undefined {
  if (error) {
    return `${id}-error`;
  }
  if (hint) {
    return `${id}-hint`;
  }
  return undefined;
}
