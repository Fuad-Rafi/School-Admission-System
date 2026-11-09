import { useEffect, useMemo, useState } from 'react';

export interface Countdown {
  seconds: number;
  expired: boolean;
  label: string;
}

export function useCountdown(expiresAt: string): Countdown {
  const target = useMemo(() => new Date(expiresAt).getTime(), [expiresAt]);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => {
      setNow(Date.now());
    }, 1000);

    return () => window.clearInterval(timer);
  }, []);

  const remainingMs = Number.isFinite(target) ? Math.max(0, target - now) : 0;
  const seconds = Math.ceil(remainingMs / 1000);

  return {
    seconds,
    expired: remainingMs <= 0,
    label: formatDuration(seconds),
  };
}

export function formatDuration(totalSeconds: number): string {
  if (totalSeconds <= 0) {
    return '00:00';
  }

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;

  const paddedMinutes = String(minutes).padStart(2, '0');
  const paddedSeconds = String(seconds).padStart(2, '0');

  return hours > 0
    ? `${hours}:${paddedMinutes}:${paddedSeconds}`
    : `${paddedMinutes}:${paddedSeconds}`;
}
