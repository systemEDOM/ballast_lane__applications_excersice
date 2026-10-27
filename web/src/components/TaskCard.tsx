import { useId } from 'react';
import type { Task, TaskStatus } from '../api/types';
import { dueSummary } from '../lib/dates';
import { nextStatus, STATUS_CLASS, STATUS_LABEL } from '../lib/status';
import { CalendarIcon, CheckIcon } from './icons';

interface TaskCardProps {
  task: Task;
  busy: boolean;
  onEdit: (task: Task) => void;
  onDelete: (task: Task) => void;
  onAdvance: (task: Task, status: TaskStatus) => void;
}

export function TaskCard({ task, busy, onEdit, onDelete, onAdvance }: TaskCardProps) {
  const headingId = useId();
  const due = dueSummary(task.dueDate, task.status);
  const advance = nextStatus(task.status);

  return (
    <article className="task-card" aria-labelledby={headingId} aria-busy={busy}>
      <h3 className="task-card__title" id={headingId}>
        {task.title}
      </h3>

      {task.description ? (
        <p className="task-card__description" title={task.description}>
          {task.description}
        </p>
      ) : null}

      <div className="task-card__meta">
        <span className={`chip chip--${STATUS_CLASS[task.status]}`}>
          <span className="chip__dot" aria-hidden="true" />
          {STATUS_LABEL[task.status]}
        </span>
        {due ? (
          <span className={`due due--${due.tone}`} title={`Due ${due.full}`}>
            <CalendarIcon className="due__icon" />
            {due.label}
          </span>
        ) : null}
      </div>

      <div className="task-card__actions">
        {advance ? (
          <button
            type="button"
            className="btn btn--step btn--sm"
            onClick={() => onAdvance(task, advance.status)}
            disabled={busy}
            aria-label={`${advance.label}: ${task.title}`}
          >
            {advance.label}
          </button>
        ) : (
          <span className="task-card__complete">
            <CheckIcon className="task-card__complete-icon" />
            Completed
          </span>
        )}
        <button
          type="button"
          className="btn btn--ghost btn--sm"
          onClick={() => onEdit(task)}
          disabled={busy}
          aria-label={`Edit: ${task.title}`}
        >
          Edit
        </button>
        <button
          type="button"
          className="btn btn--ghost btn--sm task-card__delete"
          onClick={() => onDelete(task)}
          disabled={busy}
          aria-label={`Delete: ${task.title}`}
        >
          Delete
        </button>
      </div>
    </article>
  );
}
