import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../auth/context';
import { Brand } from '../components/Brand';

function SessionSplash() {
  return (
    <div className="splash">
      <Brand />
      <div className="splash__status" role="status" aria-live="polite">
        <span className="splash__dot" aria-hidden="true" />
        Checking your session
      </div>
    </div>
  );
}

export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth();

  if (status === 'checking') {
    return <SessionSplash />;
  }
  if (status === 'anonymous') {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

export function PublicOnly({ children }: { children: ReactNode }) {
  const { status } = useAuth();

  if (status === 'checking') {
    return <SessionSplash />;
  }
  if (status === 'authenticated') {
    return <Navigate to="/tasks" replace />;
  }
  return <>{children}</>;
}
