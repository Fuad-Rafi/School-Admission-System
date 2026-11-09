import { useEffect, useRef } from 'react';
import { useCountdown } from '../hooks/useCountdown';
import type { MyHold } from '../types';
import { formatUtc } from '../utils/time';
import { StatusBadge } from './StatusBadge';

interface HoldCardProps {
  hold: MyHold;
  pending: boolean;
  onConfirm: (hold: MyHold) => void;
  onCancel: (hold: MyHold) => void;
  onExpired: () => void;
}

export function HoldCard({
  hold,
  pending,
  onConfirm,
  onCancel,
  onExpired,
}: HoldCardProps) {
  const countdown = useCountdown(hold.expiresAt);
  const expired = hold.status === 'HELD' && countdown.expired;
  const status = expired ? 'EXPIRED' : hold.status;
  const urgent =
    hold.status === 'HELD' && !countdown.expired && countdown.seconds <= 60;
  const notifiedRef = useRef(false);

  useEffect(() => {
    if (expired && !notifiedRef.current) {
      notifiedRef.current = true;
      onExpired();
    }
  }, [expired, onExpired]);

  const isHeld = hold.status === 'HELD';
  const actionsDisabled = pending || expired;

  return (
    <article className="card hold-card" data-testid={`hold-${hold.holdId}`}>
      <header className="card-header">
        <h3>{hold.className}</h3>
        <StatusBadge status={status} />
      </header>

      {status === 'HELD' ? (
        <p className={`countdown${urgent ? ' countdown-urgent' : ''}`}>
          Expires in <strong>{countdown.label}</strong>{' '}
          <span className="muted">({formatUtc(hold.expiresAt)})</span>
        </p>
      ) : (
        <p className="muted">Deadline was {formatUtc(hold.expiresAt)}</p>
      )}

      <div className="card-actions">
        {isHeld ? (
          <>
            <button
              type="button"
              className="btn btn-primary"
              disabled={actionsDisabled}
              onClick={() => onConfirm(hold)}
            >
              Pay admission fee
            </button>
            <button
              type="button"
              className="btn btn-secondary"
              disabled={actionsDisabled}
              onClick={() => onCancel(hold)}
            >
              Cancel
            </button>
          </>
        ) : null}

        {status === 'EXPIRED' ? (
          <p className="hint">
            The hold expired. You can apply again from the Classes tab.
          </p>
        ) : null}
        {status === 'CONFIRMED' ? (
          <p className="hint">Admission confirmed.</p>
        ) : null}
        {status === 'CANCELLED' ? (
          <p className="hint">Application cancelled.</p>
        ) : null}
      </div>
    </article>
  );
}
