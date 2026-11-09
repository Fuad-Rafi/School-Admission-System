import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { renderApp } from './render-app';

describe('student identity', () => {
  it('persists the 17-digit id across a remount', async () => {
    const user = userEvent.setup();
    const firstRender = renderApp();

    const input = await screen.findByLabelText('Birth registration number');
    await user.type(input, '20152691234567890');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(localStorage.getItem('sas.studentId')).toBe('20152691234567890');
    expect(await screen.findByTestId('student-id-badge')).toHaveTextContent(
      '20152691234567890',
    );

    firstRender.unmount();
    renderApp();

    expect(await screen.findByTestId('student-id-badge')).toHaveTextContent(
      '20152691234567890',
    );
  });
});
