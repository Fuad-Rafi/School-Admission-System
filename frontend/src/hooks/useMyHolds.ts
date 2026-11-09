import { useCallback } from 'react';
import { api } from '../api/client';
import type { MyHold } from '../types';
import { usePolling } from './usePolling';

export function useMyHolds(studentId: string) {
  const fetchHolds = useCallback(
    (signal: AbortSignal) =>
      studentId
        ? api.getMyHolds(studentId, signal)
        : Promise.resolve<MyHold[]>([]),
    [studentId],
  );

  return usePolling<MyHold[]>(fetchHolds, 2000, Boolean(studentId), studentId);
}
