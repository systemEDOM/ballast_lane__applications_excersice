import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { Task, TaskInput, TaskStatus } from '../api/types';
import { changeTaskStatus, createTask, deleteTask, listTasks, updateTask } from '../api/tasks';
import { AppHeader } from '../components/AppHeader';
import { BoardSkeleton } from '../components/BoardSkeleton';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { TaskCard } from '../components/TaskCard';
import { TaskFormDialog } from '../components/TaskFormDialog';
import { useToast } from '../components/ToastProvider';
import { CloseIcon, PlusIcon, SearchIcon } from '../components/icons';
import { isOverdue } from '../lib/dates';
import { errorMessage } from '../lib/errors';
import { nextStatus, STATUS_CLASS, STATUS_LABEL, STATUS_ORDER } from '../lib/status';

type StatusFilter = 'all' | TaskStatus;

const STATUS_FILTERS: { value: StatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'Todo', label: STATUS_LABEL.Todo },
  { value: 'InProgress', label: STATUS_LABEL.InProgress },
  { value: 'Done', label: STATUS_LABEL.Done },
];

const COLUMN_NOTES: Record<TaskStatus, string> = {
  Todo: 'Nothing waiting. Add a task when something comes up.',
  InProgress: 'Nothing in progress right now.',
  Done: 'Finished tasks collect here.',
};

const EMPTY_GROUPED: Record<TaskStatus, Task[]> = {
  Todo: [],
  InProgress: [],
  Done: [],
};

function compareTasks(a: Task, b: Task): number {
  if (a.dueDate && b.dueDate && a.dueDate !== b.dueDate) {
    return a.dueDate.localeCompare(b.dueDate);
  }
  if (a.dueDate && !b.dueDate) {
    return -1;
  }
  if (!a.dueDate && b.dueDate) {
    return 1;
  }
  return a.createdAt.localeCompare(b.createdAt);
}

interface StatusChange {
  id: string;
  status: TaskStatus;
}

export function TasksPage() {
  const queryClient = useQueryClient();
  const { notify } = useToast();

  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<StatusFilter>('all');
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Task | null>(null);
  const [deleting, setDeleting] = useState<Task | null>(null);

  const tasksQuery = useQuery({
    queryKey: ['tasks'],
    queryFn: ({ signal }) => listTasks(signal),
  });

  const createMutation = useMutation({
    mutationFn: (input: TaskInput) => createTask(input),
    onSuccess: (created) => {
      queryClient.setQueryData<Task[]>(['tasks'], (current) => (current ? [...current, created] : [created]));
      notify({ tone: 'success', message: 'Task created.' });
      void queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, input }: { id: string; input: TaskInput }) => updateTask(id, input),
    onSuccess: (updated) => {
      queryClient.setQueryData<Task[]>(['tasks'], (current) =>
        current ? current.map((task) => (task.id === updated.id ? updated : task)) : [updated],
      );
      notify({ tone: 'success', message: 'Task updated.' });
      void queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const statusMutation = useMutation({
    mutationFn: ({ id, status }: StatusChange) => changeTaskStatus(id, status),
    onMutate: async ({ id, status }) => {
      await queryClient.cancelQueries({ queryKey: ['tasks'] });
      const previous = queryClient.getQueryData<Task[]>(['tasks']);
      queryClient.setQueryData<Task[]>(['tasks'], (current) =>
        current ? current.map((task) => (task.id === id ? { ...task, status } : task)) : current,
      );
      return { previous };
    },
    onError: (error, _change, context) => {
      if (context?.previous) {
        queryClient.setQueryData<Task[]>(['tasks'], context.previous);
      }
      notify({ tone: 'error', message: errorMessage(error) });
    },
    onSuccess: (_task, change) => {
      notify({ tone: 'success', message: `Moved to ${STATUS_LABEL[change.status].toLowerCase()}.` });
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteTask(id),
    onSuccess: (_result, id) => {
      queryClient.setQueryData<Task[]>(['tasks'], (current) => current?.filter((task) => task.id !== id));
      notify({ tone: 'success', message: 'Task deleted.' });
      setDeleting(null);
      void queryClient.invalidateQueries({ queryKey: ['tasks'] });
    },
    onError: (error) => {
      notify({ tone: 'error', message: errorMessage(error) });
    },
  });

  const tasks = useMemo(() => tasksQuery.data ?? [], [tasksQuery.data]);

  const overdueCount = useMemo(
    () => tasks.filter((task) => isOverdue(task.dueDate, task.status)).length,
    [tasks],
  );

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    return tasks.filter((task) => {
      if (filter !== 'all' && task.status !== filter) {
        return false;
      }
      if (term.length === 0) {
        return true;
      }
      return (
        task.title.toLowerCase().includes(term) ||
        (task.description ?? '').toLowerCase().includes(term)
      );
    });
  }, [tasks, filter, search]);

  const grouped = useMemo(() => {
    const next: Record<TaskStatus, Task[]> = {
      Todo: [...EMPTY_GROUPED.Todo],
      InProgress: [...EMPTY_GROUPED.InProgress],
      Done: [...EMPTY_GROUPED.Done],
    };
    for (const task of filtered) {
      next[task.status].push(task);
    }
    for (const status of STATUS_ORDER) {
      next[status].sort(compareTasks);
    }
    return next;
  }, [filtered]);

  const isFiltering = filter !== 'all' || search.trim().length > 0;

  const summary = tasksQuery.isError
    ? 'Board unavailable'
    : isFiltering
      ? `Showing ${filtered.length} of ${tasks.length} ${tasks.length === 1 ? 'task' : 'tasks'}`
      : `${tasks.length} ${tasks.length === 1 ? 'task' : 'tasks'}${overdueCount > 0 ? ` · ${overdueCount} overdue` : ''}`;

  const clearFilters = () => {
    setFilter('all');
    setSearch('');
  };

  const handleCreate = async (input: TaskInput) => {
    await createMutation.mutateAsync(input);
  };

  const handleUpdate = async (input: TaskInput) => {
    if (!editing) {
      return;
    }
    await updateMutation.mutateAsync({ id: editing.id, input });
  };

  const handleAdvance = (task: Task, status: TaskStatus) => {
    if (!nextStatus(task.status)) {
      return;
    }
    statusMutation.mutate({ id: task.id, status });
  };

  const pendingStatusId = statusMutation.isPending ? statusMutation.variables?.id : null;

  return (
    <div className="page">
      <a className="skip-link" href="#board">
        Skip to your tasks
      </a>

      <AppHeader />

      <main className="board-main" id="board" tabIndex={-1}>
        <div className="board-head">
          <div>
            <h1 className="board-title">Your tasks</h1>
            <p className="board-meta" aria-live="polite">
              {tasksQuery.isPending ? 'Loading your board' : summary}
            </p>
          </div>
          {isFiltering && filtered.length > 0 ? (
            <button type="button" className="btn btn--quiet btn--sm" onClick={clearFilters}>
              Clear filters
            </button>
          ) : null}
        </div>

        <div className="toolbar">
          <div className="toolbar__search">
            <label htmlFor="task-search" className="visually-hidden">
              Search tasks
            </label>
            <SearchIcon className="toolbar__search-icon" />
            <input
              id="task-search"
              className="input input--search"
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search tasks"
              autoComplete="off"
            />
            {search.length > 0 ? (
              <button
                type="button"
                className="toolbar__clear"
                onClick={() => setSearch('')}
                aria-label="Clear search"
              >
                <CloseIcon />
              </button>
            ) : null}
          </div>

          <div className="segmented" role="group" aria-label="Filter by status">
            {STATUS_FILTERS.map((option) => (
              <button
                key={option.value}
                type="button"
                className={filter === option.value ? 'segmented__option is-active' : 'segmented__option'}
                aria-pressed={filter === option.value}
                onClick={() => setFilter(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>

          <button type="button" className="btn btn--primary toolbar__new" onClick={() => setCreating(true)}>
            <PlusIcon />
            New task
          </button>
        </div>

        {tasksQuery.isError && tasksQuery.data ? (
          <div className="alert alert--error alert--inline" role="status" aria-live="polite">
            <span>{errorMessage(tasksQuery.error)}</span>
            <button
              type="button"
              className="btn btn--quiet btn--sm"
              onClick={() => void tasksQuery.refetch()}
              disabled={tasksQuery.isFetching}
            >
              {tasksQuery.isFetching ? 'Retrying…' : 'Try again'}
            </button>
          </div>
        ) : null}

        {tasksQuery.isPending ? (
          <div>
            <p className="visually-hidden" role="status">
              Loading your tasks
            </p>
            <BoardSkeleton />
          </div>
        ) : tasksQuery.isError ? (
          <div className="state-panel" role="status" aria-live="polite">
            <p className="eyebrow">Connection problem</p>
            <h2 className="state-panel__title">{errorMessage(tasksQuery.error)}</h2>
            <p className="state-panel__text">
              Your tasks are safe. Check that the API is running, then try again.
            </p>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => void tasksQuery.refetch()}
              disabled={tasksQuery.isFetching}
            >
              {tasksQuery.isFetching ? 'Retrying…' : 'Try again'}
            </button>
          </div>
        ) : tasks.length === 0 ? (
          <div className="state-panel">
            <p className="eyebrow">Empty board</p>
            <h2 className="state-panel__title">Your board is clear</h2>
            <p className="state-panel__text">Add your first task and it will appear in To do.</p>
            <button type="button" className="btn btn--primary" onClick={() => setCreating(true)}>
              <PlusIcon />
              New task
            </button>
          </div>
        ) : filtered.length === 0 ? (
          <div className="state-panel">
            <p className="eyebrow">No matches</p>
            <h2 className="state-panel__title">No tasks match those filters</h2>
            <p className="state-panel__text">
              Try a different search, or clear the status filter to see the whole board.
            </p>
            <button type="button" className="btn btn--primary" onClick={clearFilters}>
              Clear filters
            </button>
          </div>
        ) : (
          <div className="board">
            {STATUS_ORDER.map((status) => (
              <section className="column" key={status} aria-labelledby={`column-${status}`}>
                <header className="column__head">
                  <h2 className="column__title" id={`column-${status}`}>
                    <span className={`column__dot column__dot--${STATUS_CLASS[status]}`} aria-hidden="true" />
                    {STATUS_LABEL[status]}
                  </h2>
                  <span className="column__count">{grouped[status].length}</span>
                </header>
                <div className="column__list">
                  {grouped[status].map((task) => (
                    <TaskCard
                      key={task.id}
                      task={task}
                      busy={pendingStatusId === task.id}
                      onEdit={setEditing}
                      onDelete={setDeleting}
                      onAdvance={handleAdvance}
                    />
                  ))}
                  {grouped[status].length === 0 ? (
                    <p className="column__empty">{COLUMN_NOTES[status]}</p>
                  ) : null}
                </div>
              </section>
            ))}
          </div>
        )}
      </main>

      {creating ? (
        <TaskFormDialog task={null} onClose={() => setCreating(false)} onSave={handleCreate} />
      ) : null}

      {editing ? (
        <TaskFormDialog
          key={editing.id}
          task={editing}
          onClose={() => setEditing(null)}
          onSave={handleUpdate}
        />
      ) : null}

      {deleting ? (
        <ConfirmDialog
          task={deleting}
          pending={deleteMutation.isPending}
          onClose={() => {
            if (!deleteMutation.isPending) {
              setDeleting(null);
            }
          }}
          onConfirm={() => {
            deleteMutation.mutate(deleting.id);
          }}
        />
      ) : null}
    </div>
  );
}
