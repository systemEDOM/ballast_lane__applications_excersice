import { useAuth } from '../auth/context';
import { Brand } from './Brand';

export function AppHeader() {
  const { user, logout } = useAuth();

  const initial = user?.displayName.trim().charAt(0).toUpperCase() ?? '?';

  return (
    <header className="app-header">
      <div className="app-header__inner">
        <Brand />
        <div className="app-header__account">
          <span className="avatar" aria-hidden="true">
            {initial}
          </span>
          <span className="app-header__user" title={user?.email}>
            {user?.displayName}
          </span>
          <button type="button" className="btn btn--quiet btn--sm" onClick={logout}>
            Sign out
          </button>
        </div>
      </div>
    </header>
  );
}
