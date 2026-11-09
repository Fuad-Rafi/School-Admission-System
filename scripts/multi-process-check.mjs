#!/usr/bin/env node
/**
 * Bonus multi-process check.
 *
 * Creates a class on the primary backend, fires many simultaneous applications split across
 * two backend processes that share one PostgreSQL database, then verifies that exactly
 * `seats` requests succeeded and that both processes report the same occupancy.
 *
 * Usage: node scripts/multi-process-check.mjs [primary] [secondary] [seats] [requests]
 */

const primary = process.argv[2] ?? 'http://localhost:8080';
const secondary = process.argv[3] ?? 'http://localhost:8081';
const seats = Number(process.argv[4] ?? 40);
const requests = Number(process.argv[5] ?? 200);

if (!Number.isInteger(seats) || seats < 1 || !Number.isInteger(requests) || requests <= seats) {
  throw new Error('seats must be >= 1 and requests must be > seats');
}

const json = async (response) => {
  const text = await response.text();
  return text ? JSON.parse(text) : null;
};

const create = await fetch(`${primary}/classes`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    name: `Multi-process check ${new Date().toISOString()}`,
    totalSeats: seats,
  }),
});

if (create.status !== 201) {
  throw new Error(`create class failed: ${create.status} ${await create.text()}`);
}

const createdClass = await json(create);
const stamp = String(Date.now()).slice(-9);
const studentIds = Array.from(
  { length: requests },
  (_, index) => `2015${stamp}${String(index).padStart(4, '0')}`,
);

const statuses = await Promise.all(
  studentIds.map((studentId, index) => {
    const base = index % 2 === 0 ? primary : secondary;
    return fetch(`${base}/classes/${createdClass.id}/holds`, {
      method: 'POST',
      headers: { 'X-Student-Id': studentId },
    }).then((response) => response.status);
  }),
);

const counts = statuses.reduce((result, status) => {
  result[status] = (result[status] ?? 0) + 1;
  return result;
}, {});

const primaryClass = (await json(await fetch(`${primary}/classes/${createdClass.id}`)));
const secondaryClass = (await json(await fetch(`${secondary}/classes/${createdClass.id}`)));

const errors = [];
if ((counts[201] ?? 0) !== seats) {
  errors.push(`expected ${seats} successful holds, got ${counts[201] ?? 0}`);
}
if ((counts[409] ?? 0) !== requests - seats) {
  errors.push(`expected ${requests - seats} conflicts, got ${counts[409] ?? 0}`);
}
if (statuses.some((status) => status >= 500)) {
  errors.push('at least one request returned 5xx');
}
for (const [label, state] of [
  ['primary', primaryClass],
  ['secondary', secondaryClass],
]) {
  if (state.heldSeats !== seats || state.availableSeats !== 0) {
    errors.push(
      `${label} reports heldSeats=${state.heldSeats}, availableSeats=${state.availableSeats}`,
    );
  }
}

console.log(
  JSON.stringify(
    {
      classId: createdClass.id,
      seats,
      requests,
      statusCounts: counts,
      primary: primaryClass,
      secondary: secondaryClass,
      errors,
    },
    null,
    2,
  ),
);

if (errors.length > 0) {
  process.exitCode = 1;
}
