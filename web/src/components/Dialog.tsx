import { useEffect, useRef } from 'react';
import type { ReactNode, RefObject } from 'react';
import { createPortal } from 'react-dom';

const FOCUSABLE_SELECTOR = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(', ');

function getFocusable(container: HTMLElement | null): HTMLElement[] {
  if (!container) {
    return [];
  }
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
    (element) => element.getClientRects().length > 0,
  );
}

interface DialogProps {
  labelledBy: string;
  describedBy?: string;
  onClose: () => void;
  initialFocusRef?: RefObject<HTMLElement | null>;
  className?: string;
  children: ReactNode;
}

export function Dialog({ labelledBy, describedBy, onClose, initialFocusRef, className, children }: DialogProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const pressedOverlay = useRef(false);
  onCloseRef.current = onClose;

  useEffect(() => {
    const panel = panelRef.current;
    restoreRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const body = document.body;
    const previousOverflow = body.style.overflow;
    const previousPadding = body.style.paddingRight;
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    body.style.overflow = 'hidden';
    if (scrollbarWidth > 0) {
      body.style.paddingRight = `${scrollbarWidth}px`;
    }

    const target = initialFocusRef?.current ?? getFocusable(panel)[0] ?? panel;
    target?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onCloseRef.current();
        return;
      }
      if (event.key !== 'Tab') {
        return;
      }
      const focusable = getFocusable(panel);
      if (focusable.length === 0) {
        event.preventDefault();
        panel?.focus();
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement;
      if (!event.shiftKey && active === last) {
        event.preventDefault();
        first?.focus();
      } else if (event.shiftKey && (active === first || !panel?.contains(active))) {
        event.preventDefault();
        last?.focus();
      }
    };

    const previousActive = restoreRef.current;
    document.addEventListener('keydown', handleKeyDown, true);

    return () => {
      document.removeEventListener('keydown', handleKeyDown, true);
      body.style.overflow = previousOverflow;
      body.style.paddingRight = previousPadding;
      if (previousActive?.isConnected) {
        previousActive.focus();
      } else {
        document.querySelector('main')?.focus();
      }
    };
  }, [initialFocusRef]);

  return createPortal(
    <div
      className="overlay"
      onMouseDown={(event) => {
        pressedOverlay.current = event.target === event.currentTarget;
      }}
      onClick={(event) => {
        if (pressedOverlay.current && event.target === event.currentTarget) {
          onCloseRef.current();
        }
        pressedOverlay.current = false;
      }}
    >
      <div
        ref={panelRef}
        className={className ? `dialog ${className}` : 'dialog'}
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        aria-describedby={describedBy}
        tabIndex={-1}
      >
        {children}
      </div>
    </div>,
    document.body,
  );
}
