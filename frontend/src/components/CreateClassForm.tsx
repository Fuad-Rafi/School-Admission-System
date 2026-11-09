import { useState, type FormEvent } from 'react';
import { toUserMessage } from '../api/client';

interface CreateClassFormProps {
  onCreate: (name: string, totalSeats: number) => Promise<void>;
}

export function CreateClassForm({ onCreate }: CreateClassFormProps) {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('120');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = name.trim();
    const totalSeats = Number(seats);

    if (trimmedName.length < 1 || trimmedName.length > 200) {
      setError('Enter a class name between 1 and 200 characters.');
      return;
    }

    if (!Number.isInteger(totalSeats) || totalSeats < 1 || totalSeats > 10000) {
      setError('Seats must be a whole number between 1 and 10000.');
      return;
    }

    setBusy(true);
    setError(null);

    try {
      await onCreate(trimmedName, totalSeats);
      setName('');
      setSeats('120');
    } catch (cause) {
      setError(toUserMessage(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="create-form" onSubmit={handleSubmit} noValidate>
      <div className="field">
        <label htmlFor="class-name">Class name</label>
        <input
          id="class-name"
          value={name}
          maxLength={200}
          placeholder="Class 6 - Morning Shift"
          onChange={(event) => setName(event.target.value)}
        />
      </div>

      <div className="field field-small">
        <label htmlFor="class-seats">Total seats</label>
        <input
          id="class-seats"
          type="number"
          min={1}
          max={10000}
          value={seats}
          onChange={(event) => setSeats(event.target.value)}
        />
      </div>

      <button type="submit" className="btn btn-primary" disabled={busy}>
        {busy ? 'Creating…' : 'Create class'}
      </button>

      {error ? (
        <p className="error-text" role="alert">
          {error}
        </p>
      ) : null}
    </form>
  );
}
