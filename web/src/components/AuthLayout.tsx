import type { ReactNode } from 'react';
import { Brand } from './Brand';

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="auth">
      <div className="auth__form">
        <div className="auth__brand">
          <Brand />
        </div>
        <div className="auth__inner">{children}</div>
      </div>

      <aside className="auth__panel">
        <div className="auth__panel-inner">
          <p className="eyebrow">Task board</p>
          <h2 className="auth__panel-title">Your week, at a glance.</h2>
          <p className="auth__panel-text">
            Every task moves from to do, to in progress, to done. Nothing else to learn.
          </p>

          <div className="mini-board" aria-hidden="true">
            <div className="mini-board__column">
              <span className="mini-board__label">To do</span>
              <div className="mini-card">
                <span className="mini-line mini-line--long" />
                <span className="mini-line mini-line--short" />
              </div>
              <div className="mini-card">
                <span className="mini-line mini-line--medium" />
              </div>
            </div>
            <div className="mini-board__column">
              <span className="mini-board__label">In progress</span>
              <div className="mini-card mini-card--active">
                <span className="mini-line mini-line--long" />
                <span className="mini-line mini-line--short" />
              </div>
            </div>
            <div className="mini-board__column">
              <span className="mini-board__label">Done</span>
              <div className="mini-card mini-card--done">
                <span className="mini-line mini-line--medium" />
              </div>
            </div>
          </div>
        </div>
      </aside>
    </div>
  );
}
