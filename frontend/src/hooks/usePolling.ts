import { useCallback, useEffect, useRef, useState } from 'react';
import { isAbortError, toUserMessage } from '../api/client';

export interface PollingResult<T> {
  data: T | null;
  error: string | null;
  loading: boolean;
  refresh: () => Promise<void>;
}

/**
 * Fetches immediately, then on an interval. Polling pauses while the tab is hidden and
 * resumes on focus. Failed polls keep the last good data and expose a readable error.
 */
export function usePolling<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  intervalMs: number,
  enabled = true,
  refreshKey: unknown = null,
): PollingResult<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(enabled);

  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  const inFlight = useRef(false);
  const controllerRef = useRef<AbortController | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;

    return () => {
      mountedRef.current = false;
      controllerRef.current?.abort();
    };
  }, []);

  const refresh = useCallback(async () => {
    if (!enabled || inFlight.current) {
      return;
    }

    inFlight.current = true;
    const controller = new AbortController();
    controllerRef.current = controller;

    try {
      const result = await fetcherRef.current(controller.signal);

      if (!controller.signal.aborted && mountedRef.current) {
        setData(result);
        setError(null);
      }
    } catch (cause) {
      if (!controller.signal.aborted && mountedRef.current && !isAbortError(cause)) {
        setError(toUserMessage(cause));
      }
    } finally {
      inFlight.current = false;

      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) {
      setData(null);
      setError(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    void refresh();

    const timer = window.setInterval(() => {
      if (!document.hidden) {
        void refresh();
      }
    }, intervalMs);

    const onVisibilityChange = () => {
      if (!document.hidden) {
        void refresh();
      }
    };

    document.addEventListener('visibilitychange', onVisibilityChange);

    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisibilityChange);
      controllerRef.current?.abort();
      controllerRef.current = null;
      inFlight.current = false;
    };
  }, [enabled, intervalMs, refresh, refreshKey]);

  return { data, error, loading, refresh };
}
