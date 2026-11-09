import type { ClassInfo } from '../types';

interface ClassCardProps {
  classInfo: ClassInfo;
  disabledReason: string | null;
  pending: boolean;
  onApply: (classId: string) => void;
}

export function ClassCard({
  classInfo,
  disabledReason,
  pending,
  onApply,
}: ClassCardProps) {
  return (
    <article className="card class-card">
      <header className="card-header">
        <h3>{classInfo.name}</h3>
        <span className="muted">{classInfo.totalSeats} seats</span>
      </header>

      <dl className="seat-grid">
        <div className="seat-stat">
          <dt>Total</dt>
          <dd>{classInfo.totalSeats}</dd>
        </div>
        <div className="seat-stat">
          <dt>Held</dt>
          <dd>{classInfo.heldSeats}</dd>
        </div>
        <div className="seat-stat">
          <dt>Confirmed</dt>
          <dd>{classInfo.confirmedSeats}</dd>
        </div>
        <div className="seat-stat">
          <dt>Available</dt>
          <dd data-testid="available-seats">{classInfo.availableSeats}</dd>
        </div>
      </dl>

      <div className="card-actions">
        <button
          type="button"
          className="btn btn-primary"
          disabled={Boolean(disabledReason) || pending}
          onClick={() => onApply(classInfo.id)}
        >
          {pending ? 'Applying…' : 'Apply'}
        </button>
      </div>

      {disabledReason ? <p className="hint">{disabledReason}</p> : null}
    </article>
  );
}
