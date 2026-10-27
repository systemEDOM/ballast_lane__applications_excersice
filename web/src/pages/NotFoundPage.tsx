import { Link } from 'react-router-dom';
import { Brand } from '../components/Brand';

export function NotFoundPage() {
  return (
    <div className="splash">
      <Brand />
      <div className="not-found">
        <p className="eyebrow">Page not found</p>
        <h1 className="not-found__title">That page is not on the board</h1>
        <p className="not-found__text">The link may be out of date.</p>
        <Link className="btn btn--primary" to="/tasks">
          Go to your tasks
        </Link>
      </div>
    </div>
  );
}
