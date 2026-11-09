import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderApp } from './render-app';

describe('classes screen', () => {
  it('renders live seat counts and enables Apply when a seat is available', async () => {
    localStorage.setItem('sas.studentId', '20152691234567890');

    renderApp();

    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument();
    expect(screen.getByTestId('available-seats')).toHaveTextContent('25');
    expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled();
  });
});
