import { BoardMark } from './icons';

export function Brand() {
  return (
    <span className="brand">
      <BoardMark className="brand__mark" />
      <span className="brand__name">Ballast</span>
    </span>
  );
}
