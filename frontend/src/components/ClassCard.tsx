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
  const totalSeats = Math.max(0, classInfo.totalSeats);
  const heldPercent =
    totalSeats === 0 ? 0 : Math.min(100, (classInfo.heldSeats / totalSeats) * 100);
  const confirmedPercent =
    totalSeats === 0
      ? 0
      : Math.min(100 - heldPercent, (classInfo.confirmedSeats / totalSeats) * 100);

  return (
    <article className="card class-card">
      <header className="card-header">
        <h3>{classInfo.name}</h3>
        <span className="muted">{classInfo.totalSeats} seats</span>
      </header>

      <div className="seat-meter" aria-hidden="true">
        <span className="seat-meter-held" style={{ width: `${heldPercent}%` }} />
        <span
          className="seat-meter-confirmed"
          style={{ width: `${confirmedPercent}%` }}
        />
      </div>

      <div className="seat-legend" aria-hidden="true">
        <span>
          <i className="legend-dot legend-held" /> Held
        </span>
        <span>
          <i className="legend-dot legend-confirmed" /> Confirmed
        </span>
        <span>
          <i className="legend-dot legend-available" /> Available
        </span>
      </div>

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
        <div className="seat-stat seat-stat-available">
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
