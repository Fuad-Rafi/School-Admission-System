import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { API } from './handlers';
import { renderApp } from './render-app';
import { server } from './server';

describe('apply flow', () => {
  beforeEach(() => {
    localStorage.setItem('sas.studentId', '20152691234567890');
  });

  it('creates a hold and navigates to My Applications', async () => {
    const applySpy = vi.fn();

    server.use(
      http.post(`${API}/classes/c1/holds`, async ({ request }) => {
        applySpy(request.headers.get('X-Student-Id'));

        return HttpResponse.json(
          {
            holdId: 'h1',
            classId: 'c1',
            studentId: '20152691234567890',
            status: 'HELD',
            expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
          },
          { status: 201 },
        );
      }),
    );

    const user = userEvent.setup();
    renderApp();

    await screen.findByText('Class 6 - Morning Shift');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(
      await screen.findByText(/application created/i),
    ).toBeInTheDocument();
    expect(applySpy).toHaveBeenCalledWith('20152691234567890');
    expect(window.location.pathname).toBe('/my-holds');
  });
});
