import { useRef, useState } from 'react';
import type { FormEvent, RefObject } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../auth/context';
import { AuthLayout } from '../components/AuthLayout';
import { describedBy, Field } from '../components/Field';
import { mapFieldErrors, validateLogin } from '../lib/validation';
import type { FormErrors, LoginField } from '../lib/validation';

const DEMO_EMAIL = 'demo@ballastlane.test';
const DEMO_PASSWORD = 'Demo12345!';
const LOGIN_FIELDS: readonly LoginField[] = ['email', 'password'];

export function LoginPage() {
  const { login } = useAuth();
  const [values, setValues] = useState({ email: '', password: '' });
  const [errors, setErrors] = useState<FormErrors<LoginField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const refs: Record<LoginField, RefObject<HTMLInputElement | null>> = {
    email: emailRef,
    password: passwordRef,
  };

  const focusFirstError = (fieldErrors: FormErrors<LoginField>) => {
    for (const field of LOGIN_FIELDS) {
      if (fieldErrors[field]) {
        refs[field].current?.focus();
        return;
      }
    }
  };

  const signIn = async (email: string, password: string) => {
    setPending(true);
    setFormError(null);
    try {
      await login(email, password);
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 401) {
          setFormError('Email or password is incorrect.');
        } else {
          const mapped = mapFieldErrors(error.fieldErrors, LOGIN_FIELDS);
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

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validation = validateLogin(values);
    setErrors(validation);
    if (Object.keys(validation).length > 0) {
      focusFirstError(validation);
      return;
    }
    void signIn(values.email.trim(), values.password);
  };

  const handleDemo = () => {
    setValues({ email: DEMO_EMAIL, password: DEMO_PASSWORD });
    setErrors({});
    void signIn(DEMO_EMAIL, DEMO_PASSWORD);
  };

  return (
    <AuthLayout>
      <div className="auth__heading">
        <p className="eyebrow">Welcome back</p>
        <h1 className="auth__title">Sign in</h1>
        <p className="auth__subtitle">Pick up where your board left off.</p>
      </div>

      <form className="form" onSubmit={handleSubmit} noValidate aria-busy={pending}>
        {formError ? (
          <div className="alert alert--error" role="status" aria-live="polite">
            {formError}
          </div>
        ) : null}

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
            onFocus={(event) => event.currentTarget.select()}
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
              autoComplete="current-password"
              value={values.password}
              maxLength={128}
              onChange={(event) => {
                setValues((current) => ({ ...current, password: event.target.value }));
                setErrors((current) => ({ ...current, password: undefined }));
              }}
              aria-invalid={Boolean(errors.password)}
              aria-describedby={describedBy('password', errors.password)}
              onFocus={(event) => event.currentTarget.select()}
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
        </Field>

        <button type="submit" className="btn btn--primary btn--block btn--lg" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>

      <div className="demo">
        <div>
          <p className="demo__title">Demo account</p>
          <p className="demo__credentials">
            {DEMO_EMAIL} · {DEMO_PASSWORD}
          </p>
          <p className="demo__note">Fills the form and signs you in to the seeded board.</p>
        </div>
        <button type="button" className="btn btn--quiet btn--sm" onClick={handleDemo} disabled={pending}>
          Use demo account
        </button>
      </div>

      <p className="auth__switch">
        New here? <Link to="/register">Create an account</Link>
      </p>
    </AuthLayout>
  );
}
