import type { HoldStatus } from '../types';

const LABELS: Record<HoldStatus, string> = {
  HELD: 'HELD',
  CONFIRMED: 'CONFIRMED',
  EXPIRED: 'EXPIRED',
  CANCELLED: 'CANCELLED',
};

export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`}>
      {LABELS[status]}
    </span>
  );
}
