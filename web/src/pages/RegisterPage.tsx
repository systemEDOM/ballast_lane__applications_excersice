import { useRef, useState } from 'react';
import type { FormEvent, RefObject } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/context';
import { AuthLayout } from '../components/AuthLayout';
import { describedBy, Field } from '../components/Field';
import { checkPassword, mapFieldErrors, PASSWORD_RULES, validateRegister } from '../lib/validation';
import type { FormErrors, RegisterField } from '../lib/validation';

const REGISTER_FIELDS: readonly RegisterField[] = ['displayName', 'email', 'password'];

export function RegisterPage() {
  const { register } = useAuth();
  const [values, setValues] = useState({ displayName: '', email: '', password: '' });
  const [errors, setErrors] = useState<FormErrors<RegisterField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const nameRef = useRef<HTMLInputElement>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const refs: Record<RegisterField, RefObject<HTMLInputElement | null>> = {
    displayName: nameRef,
    email: emailRef,
    password: passwordRef,
  };

  const focusFirstError = (fieldErrors: FormErrors<RegisterField>) => {
    for (const field of REGISTER_FIELDS) {
      if (fieldErrors[field]) {
        refs[field].current?.focus();
        return;
      }
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validation = validateRegister(values);
    setErrors(validation);
    setFormError(null);
    if (Object.keys(validation).length > 0) {
      focusFirstError(validation);
      return;
    }

    setPending(true);
    try {
      await register({
        email: values.email.trim(),
        displayName: values.displayName.trim(),
        password: values.password,
      });
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 409) {
          setErrors({ email: error.message });
          emailRef.current?.focus();
        } else {
          const mapped = mapFieldErrors(error.fieldErrors, REGISTER_FIELDS);
          if (Object.keys(mapped).length > 0) {
            setErrors(mapped);
            focusFirstError(mapped);
          } else {
            setFormError(error.message);
          }
        }
      } else {
        setFormError('Something went wrong. Try again.');
      }
      setPending(false);
    }
  };

  const checks = checkPassword(values.password);

  return (
    <AuthLayout>
      <div className="auth__heading">
        <p className="eyebrow">Get set up</p>
        <h1 className="auth__title">Create your account</h1>
        <p className="auth__subtitle">One board, just for your tasks.</p>
      </div>

      <form className="form" onSubmit={handleSubmit} noValidate aria-busy={pending}>
        {formError ? (
          <div className="alert alert--error" role="status" aria-live="polite">
            {formError}
          </div>
        ) : null}

        <Field id="displayName" label="Display name" error={errors.displayName}>
          <input
            ref={nameRef}
            id="displayName"
            name="displayName"
            className="input"
            type="text"
            autoComplete="name"
            value={values.displayName}
            maxLength={80}
            onChange={(event) => {
              setValues((current) => ({ ...current, displayName: event.target.value }));
              setErrors((current) => ({ ...current, displayName: undefined }));
            }}
            aria-invalid={Boolean(errors.displayName)}
            aria-describedby={describedBy('displayName', errors.displayName)}
            placeholder="Alex Rivera"
          />
        </Field>

        <Field id="email" label="Email" error={errors.email}>
          <input
            ref={emailRef}
            id="email"
            name="email"
            className="input"
            type="email"
            inputMode="email"
            autoComplete="email"
            autoCapitalize="none"
            spellCheck={false}
            value={values.email}
            maxLength={254}
            onChange={(event) => {
              setValues((current) => ({ ...current, email: event.target.value }));
              setErrors((current) => ({ ...current, email: undefined }));
            }}
            aria-invalid={Boolean(errors.email)}
            aria-describedby={describedBy('email', errors.email)}
            placeholder="you@studio.com"
          />
        </Field>

        <Field id="password" label="Password" error={errors.password}>
          <div className="input-affix">
            <input
              ref={passwordRef}
              id="password"
              name="password"
              className="input"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              value={values.password}
              maxLength={128}
              onChange={(event) => {
                setValues((current) => ({ ...current, password: event.target.value }));
                setErrors((current) => ({ ...current, password: undefined }));
              }}
              aria-invalid={Boolean(errors.password)}
              aria-describedby={errors.password ? 'password-error' : 'password-rules'}
            />
            <button
              type="button"
              className="input-affix__button"
              onClick={() => setShowPassword((current) => !current)}
              aria-label={showPassword ? 'Hide password' : 'Show password'}
              aria-pressed={showPassword}
            >
              {showPassword ? 'Hide' : 'Show'}
            </button>
          </div>
          <ul className="rules" id="password-rules">
            {PASSWORD_RULES.map((rule) => (
              <li key={rule.id} className={checks[rule.id] ? 'rules__item is-met' : 'rules__item'}>
                <span className="rules__marker" aria-hidden="true" />
                {rule.label}
              </li>
            ))}
          </ul>
        </Field>

        <button type="submit" className="btn btn--primary btn--block btn--lg" disabled={pending}>
          {pending ? 'Creating account…' : 'Create account'}
        </button>
      </form>

      <p className="auth__switch">
        Already have an account? <Link to="/login">Sign in</Link>
      </p>
    </AuthLayout>
  );
}
