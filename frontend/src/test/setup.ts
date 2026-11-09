import '@testing-library/jest-dom/vitest';
import {
  Headers as UndiciHeaders,
  Request as UndiciRequest,
  Response as UndiciResponse,
  fetch as undiciFetch,
} from 'undici';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './server';

// jsdom does not provide fetch; MSW intercepts undici's implementation.
globalThis.fetch = undiciFetch as unknown as typeof globalThis.fetch;
globalThis.Headers = UndiciHeaders as unknown as typeof globalThis.Headers;
globalThis.Request = UndiciRequest as unknown as typeof globalThis.Request;
globalThis.Response = UndiciResponse as unknown as typeof globalThis.Response;

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  cleanup();
  server.resetHandlers();
  localStorage.clear();
  window.history.pushState({}, '', '/');
});

afterAll(() => {
  server.close();
});
