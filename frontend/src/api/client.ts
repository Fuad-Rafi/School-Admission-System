import { API_BASE_URL } from '../config';
import type {
  ApiErrorBody,
  ClassCreated,
  ClassInfo,
  ConfirmHoldResponse,
  HoldCreated,
  HoldInfo,
  MyHold,
} from '../types';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export class NetworkError extends Error {
  constructor() {
    super("Can't reach the server. Check your connection and try again.");
    this.name = 'NetworkError';
  }
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function toUserMessage(error: unknown): string {
  if (error instanceof ApiError || error instanceof NetworkError) {
    return error.message;
  }

  if (isAbortError(error)) {
    return '';
  }

  return 'Something went wrong. Please try again.';
}

interface RequestOptions extends RequestInit {
  studentId?: string;
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { studentId, headers, ...init } = options;
  const requestHeaders = new Headers(headers);

  if (studentId) {
    requestHeaders.set('X-Student-Id', studentId);
  }

  if (init.body && !requestHeaders.has('Content-Type')) {
    requestHeaders.set('Content-Type', 'application/json');
  }

  let response: Response;

  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      cache: 'no-store',
      ...init,
      headers: requestHeaders,
    });
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }

    throw new NetworkError();
  }

  if (!response.ok) {
    let body: Partial<ApiErrorBody> = {};

    try {
      body = (await response.json()) as Partial<ApiErrorBody>;
    } catch {
      // Non-JSON error response; fall back to a generic message.
    }

    throw new ApiError(
      response.status,
      body.code ?? 'REQUEST_FAILED',
      body.message ?? 'Something went wrong. Please try again.',
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  listClasses: (signal?: AbortSignal) =>
    request<ClassInfo[]>('/classes', { signal }),

  createClass: (name: string, totalSeats: number) =>
    request<ClassCreated>('/classes', {
      method: 'POST',
      body: JSON.stringify({ name, totalSeats }),
    }),

  apply: (classId: string, studentId: string, signal?: AbortSignal) =>
    request<HoldCreated>(`/classes/${classId}/holds`, {
      method: 'POST',
      studentId,
      signal,
    }),

  getHold: (holdId: string, signal?: AbortSignal) =>
    request<HoldInfo>(`/holds/${holdId}`, { signal }),

  confirm: (holdId: string, studentId: string) =>
    request<ConfirmHoldResponse>(`/holds/${holdId}/confirm`, {
      method: 'POST',
      studentId,
    }),

  cancel: (holdId: string, studentId: string) =>
    request<void>(`/holds/${holdId}`, {
      method: 'DELETE',
      studentId,
    }),

  getMyHolds: (studentId: string, signal?: AbortSignal) =>
    request<MyHold[]>('/me/holds', { studentId, signal }),
};
