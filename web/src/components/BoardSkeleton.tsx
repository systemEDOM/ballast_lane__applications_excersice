import { STATUS_LABEL, STATUS_ORDER } from '../lib/status';

const CARD_COUNTS = [2, 2, 1];

export function BoardSkeleton() {
  return (
    <div className="board" aria-hidden="true">
      {STATUS_ORDER.map((status, columnIndex) => (
        <section className="column" key={status}>
          <header className="column__head">
            <h2 className="column__title">{STATUS_LABEL[status]}</h2>
            <span className="column__count">–</span>
          </header>
          <div className="column__list">
            {Array.from({ length: CARD_COUNTS[columnIndex] ?? 1 }, (_, cardIndex) => (
              <div className="task-card task-card--skeleton" key={cardIndex}>
                <span className="skeleton skeleton--title" />
                <span className="skeleton skeleton--line" />
                <span className="skeleton skeleton--line skeleton--line-short" />
                <span className="skeleton skeleton--meta" />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
