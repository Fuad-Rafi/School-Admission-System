import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ClassCard } from '../components/ClassCard';
import { sampleClass } from './handlers';

describe('full class', () => {
  it('disables Apply and explains that no seats are available', () => {
    render(
      <ClassCard
        classInfo={{ ...sampleClass, availableSeats: 0 }}
        disabledReason="No seats available."
        pending={false}
        onApply={vi.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled();
    expect(screen.getByText('No seats available.')).toBeInTheDocument();
  });
});
