import { useCallback } from 'react';
import { api } from '../api/client';
import type { ClassInfo } from '../types';
import { usePolling } from './usePolling';

export function useClasses() {
  const fetchClasses = useCallback(
    (signal: AbortSignal) => api.listClasses(signal),
    [],
  );

  return usePolling<ClassInfo[]>(fetchClasses, 3000, true);
}
