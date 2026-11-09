import { http, HttpResponse } from 'msw';
import type { ClassInfo, MyHold } from '../types';

export const API = 'http://localhost:8080';

export const sampleClass: ClassInfo = {
  id: 'c1',
  name: 'Class 6 - Morning Shift',
  totalSeats: 120,
  heldSeats: 15,
  confirmedSeats: 80,
  availableSeats: 25,
};

export const sampleHold: MyHold = {
  holdId: 'h1',
  classId: 'c1',
  className: 'Class 6 - Morning Shift',
  status: 'HELD',
  expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
};

export const handlers = [
  http.get(`${API}/classes`, () => HttpResponse.json([sampleClass])),
  http.get(`${API}/me/holds`, () => HttpResponse.json([])),
];
