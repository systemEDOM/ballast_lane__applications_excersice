import { useId, useRef, useState } from 'react';
import type { FormEvent, RefObject } from 'react';
import { ApiError } from '../api/client';
import type { Task, TaskInput } from '../api/types';
import { todayIso } from '../lib/dates';
import { mapFieldErrors, validateTask } from '../lib/validation';
import type { FormErrors, TaskField, TaskFormValues } from '../lib/validation';
import { Dialog } from './Dialog';
import { describedBy, Field } from './Field';
import { CloseIcon } from './icons';

const DESCRIPTION_MAX = 1000;
const TASK_FIELDS: readonly TaskField[] = ['title', 'description', 'dueDate'];

interface TaskFormDialogProps {
  task: Task | null;
  onClose: () => void;
  onSave: (input: TaskInput) => Promise<void>;
}

export function TaskFormDialog({ task, onClose, onSave }: TaskFormDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const dueDateId = useId();
  const dialogTitleId = useId();

  const [values, setValues] = useState<TaskFormValues>(() => ({
    title: task?.title ?? '',
    description: task?.description ?? '',
    dueDate: task?.dueDate ?? '',
  }));
  const [errors, setErrors] = useState<FormErrors<TaskField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const titleRef = useRef<HTMLInputElement>(null);
  const descriptionRef = useRef<HTMLTextAreaElement>(null);
  const dueDateRef = useRef<HTMLInputElement>(null);

  const refs: Record<TaskField, RefObject<HTMLInputElement | HTMLTextAreaElement | null>> = {
    title: titleRef,
    description: descriptionRef,
    dueDate: dueDateRef,
  };

  const focusFirstError = (fieldErrors: FormErrors<TaskField>) => {
    for (const field of TASK_FIELDS) {
      if (fieldErrors[field]) {
        refs[field].current?.focus();
        return;
      }
    }
  };

  const update = <Field extends keyof TaskFormValues>(field: Field, value: TaskFormValues[Field]) => {
    setValues((current) => ({ ...current, [field]: value }));
    setErrors((current) => {
      if (!current[field]) {
        return current;
      }
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validation = validateTask(values);
    setErrors(validation);
    setFormError(null);

    if (Object.keys(validation).length > 0) {
      focusFirstError(validation);
      return;
    }

    setPending(true);
    try {
      await onSave({
        title: values.title.trim(),
        description: values.description.trim().length > 0 ? values.description.trim() : null,
        dueDate: values.dueDate.length > 0 ? values.dueDate : null,
      });
      onClose();
    } catch (error) {
      if (error instanceof ApiError) {
        const serverErrors = mapFieldErrors(error.fieldErrors, TASK_FIELDS);
        if (Object.keys(serverErrors).length > 0) {
          setErrors(serverErrors);
          focusFirstError(serverErrors);
        } else {
          setFormError(error.message);
        }
      } else {
        setFormError('Something went wrong. Try again.');
      }
    } finally {
      setPending(false);
    }
  };

  const pastDue = values.dueDate.length > 0 && values.dueDate < todayIso();
  const dueError = errors.dueDate ?? (pastDue ? 'Due date cannot be in the past. Pick a new date or clear it.' : undefined);

  return (
    <Dialog
      labelledBy={dialogTitleId}
      onClose={onClose}
      initialFocusRef={titleRef}
      className="dialog--form"
    >
      <form onSubmit={handleSubmit} noValidate aria-busy={pending}>
        <div className="dialog__head">
          <div>
            <p className="eyebrow">{task ? 'Edit task' : 'New task'}</p>
            <h2 className="dialog__title" id={dialogTitleId}>
              {task ? 'Update the details' : 'Add a task'}
            </h2>
          </div>
          <button type="button" className="icon-btn" onClick={onClose} aria-label="Close dialog">
            <CloseIcon />
          </button>
        </div>

        <div className="dialog__body">
          {formError ? (
            <div className="alert alert--error" role="status" aria-live="polite">
              {formError}
            </div>
          ) : null}

          <Field id={titleId} label="Title" error={errors.title}>
            <input
              ref={titleRef}
              id={titleId}
              className="input"
              type="text"
              value={values.title}
              maxLength={120}
              onChange={(event) => update('title', event.target.value)}
              aria-invalid={Boolean(errors.title)}
              aria-describedby={describedBy(titleId, errors.title)}
              autoComplete="off"
              placeholder="Draft the proposal"
            />
          </Field>

          <Field
            id={descriptionId}
            label="Description"
            optional
            error={errors.description}
            counter={`${values.description.length}/${DESCRIPTION_MAX}`}
          >
            <textarea
              ref={descriptionRef}
              id={descriptionId}
              className="input input--textarea"
              rows={4}
              value={values.description}
              maxLength={DESCRIPTION_MAX}
              onChange={(event) => update('description', event.target.value)}
              aria-invalid={Boolean(errors.description)}
              aria-describedby={describedBy(descriptionId, errors.description)}
              placeholder="A short note for future you"
            />
          </Field>

          <Field id={dueDateId} label="Due date" optional error={dueError}>
            <div className="field__row">
              <input
                ref={dueDateRef}
                id={dueDateId}
                className="input"
                type="date"
                value={values.dueDate}
                min={todayIso()}
                onChange={(event) => update('dueDate', event.target.value)}
                aria-invalid={Boolean(dueError)}
                aria-describedby={describedBy(dueDateId, dueError)}
              />
              {values.dueDate ? (
                <button type="button" className="btn btn--quiet btn--sm" onClick={() => update('dueDate', '')}>
                  Clear
                </button>
              ) : null}
            </div>
          </Field>
        </div>

        <div className="dialog__foot">
          <button type="button" className="btn btn--quiet" onClick={onClose} disabled={pending}>
            Cancel
          </button>
          <button type="submit" className="btn btn--primary" disabled={pending}>
            {pending ? 'Saving…' : task ? 'Save changes' : 'Create task'}
          </button>
        </div>
      </form>
    </Dialog>
  );
}
