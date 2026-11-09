import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { HoldCard } from '../components/HoldCard';
import type { MyHold } from '../types';

describe('hold countdown', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows EXPIRED and disables payment when the deadline passes', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-08T10:00:00Z'));

    const hold: MyHold = {
      holdId: 'h1',
      classId: 'c1',
      className: 'Class 6 - Morning Shift',
      status: 'HELD',
      expiresAt: '2026-11-08T10:00:02Z',
    };

    const onExpired = vi.fn();

    render(
      <HoldCard
        hold={hold}
        pending={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        onExpired={onExpired}
      />,
    );

    expect(
      screen.getByRole('button', { name: 'Pay admission fee' }),
    ).toBeEnabled();

    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(screen.getByText('EXPIRED')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Pay admission fee' }),
    ).toBeDisabled();
    expect(onExpired).toHaveBeenCalled();
  });
});
