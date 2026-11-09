import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { ErrorBanner } from '../components/ErrorBanner';
import { HoldCard } from '../components/HoldCard';
import { useMyHolds } from '../hooks/useMyHolds';
import type { Notify } from '../notifications';
import type { MyHold } from '../types';

interface MyHoldsPageProps {
  studentId: string;
  notify: Notify;
}

export function MyHoldsPage({ studentId, notify }: MyHoldsPageProps) {
  const holds = useMyHolds(studentId);
  const [pendingId, setPendingId] = useState<string | null>(null);

  const handleConfirm = async (hold: MyHold) => {
    if (!studentId) {
      return;
    }

    setPendingId(hold.holdId);

    try {
      await api.confirm(hold.holdId, studentId);
      notify('success', 'Admission confirmed.');
      await holds.refresh();
    } catch (cause) {
      notify('error', toUserMessage(cause));
      await holds.refresh();
    } finally {
      setPendingId(null);
    }
  };

  const handleCancel = async (hold: MyHold) => {
    if (!studentId) {
      return;
    }

    if (!window.confirm('Cancel this application? The seat will be released.')) {
      return;
    }

    setPendingId(hold.holdId);

    try {
      await api.cancel(hold.holdId, studentId);
      notify('success', 'Application cancelled. The seat was released.');
      await holds.refresh();
    } catch (cause) {
      notify('error', toUserMessage(cause));
      await holds.refresh();
    } finally {
      setPendingId(null);
    }
  };

  const handleExpired = useCallback(() => {
    void holds.refresh();
  }, [holds.refresh]);

  const list = holds.data ?? [];

  return (
    <div className="stack">
      <div className="section-header">
        <h2>My Applications</h2>
        <span className="live-indicator">
          <span className="live-dot" aria-hidden="true" /> Live · every 2 s
        </span>
      </div>

      {holds.error ? (
        <ErrorBanner
          message={holds.error}
          onRetry={() => {
            void holds.refresh();
          }}
        />
      ) : null}

      {!studentId ? (
        <div className="empty-state">
          <h3>Identify yourself</h3>
          <p>
            Enter your 17-digit birth registration number above to see your
            applications.
          </p>
        </div>
      ) : null}

      {studentId && holds.loading && !holds.data ? (
        <p className="muted">Loading your applications…</p>
      ) : null}

      {studentId && !holds.loading && list.length === 0 ? (
        <div className="empty-state">
          <h3>No applications yet</h3>
          <p>
            Browse the <Link to="/">classes list</Link> to apply for a seat.
          </p>
        </div>
      ) : null}

      <div className="stack">
        {list.map((hold) => (
          <HoldCard
            key={hold.holdId}
            hold={hold}
            pending={pendingId === hold.holdId}
            onConfirm={handleConfirm}
            onCancel={handleCancel}
            onExpired={handleExpired}
          />
        ))}
      </div>
    </div>
  );
}
