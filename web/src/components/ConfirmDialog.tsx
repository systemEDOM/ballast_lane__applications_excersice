import { useId, useRef } from 'react';
import type { Task } from '../api/types';
import { Dialog } from './Dialog';

interface ConfirmDialogProps {
  task: Task;
  pending: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function ConfirmDialog({ task, pending, onClose, onConfirm }: ConfirmDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);

  return (
    <Dialog
      labelledBy={titleId}
      describedBy={descriptionId}
      onClose={onClose}
      initialFocusRef={cancelRef}
      className="dialog--confirm"
    >
      <div className="dialog__head">
        <h2 className="dialog__title" id={titleId}>
          Delete this task?
        </h2>
      </div>
      <div className="dialog__body">
        <p id={descriptionId} className="dialog__text">
          “{task.title}” will be removed from your board. This cannot be undone.
        </p>
      </div>
      <div className="dialog__foot">
        <button ref={cancelRef} type="button" className="btn btn--quiet" onClick={onClose} disabled={pending}>
          Cancel
        </button>
        <button type="button" className="btn btn--danger" onClick={onConfirm} disabled={pending}>
          {pending ? 'Deleting…' : 'Delete task'}
        </button>
      </div>
    </Dialog>
  );
}
