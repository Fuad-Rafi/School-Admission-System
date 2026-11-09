import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { API } from './handlers';
import { renderApp } from './render-app';
import { server } from './server';

describe('error handling', () => {
  beforeEach(() => {
    localStorage.setItem('sas.studentId', '20152691234567890');
  });

  it('shows the server message when the class is full', async () => {
    server.use(
      http.post(`${API}/classes/c1/holds`, () =>
        HttpResponse.json(
          {
            code: 'SEAT_UNAVAILABLE',
            message: 'All seats in this class are taken.',
          },
          { status: 409 },
        ),
      ),
    );

    const user = userEvent.setup();
    renderApp();

    await screen.findByText('Class 6 - Morning Shift');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(
      await screen.findByText('All seats in this class are taken.'),
    ).toBeInTheDocument();
  });

  it('shows a readable banner when the server is unreachable', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.error()));

    renderApp();

    expect(
      await screen.findByText(/can't reach the server/i),
    ).toBeInTheDocument();
  });
});
