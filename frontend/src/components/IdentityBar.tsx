import { useEffect, useState, type FormEvent } from 'react';
import type { StudentIdentity } from '../hooks/useStudentId';

export function IdentityBar({ identity }: { identity: StudentIdentity }) {
  const [draft, setDraft] = useState(identity.studentId);
  const [editing, setEditing] = useState(!identity.studentId);

  useEffect(() => {
    setDraft(identity.studentId);
    setEditing(!identity.studentId);
  }, [identity.studentId]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (identity.save(draft)) {
      setEditing(false);
    }
  };

  return (
    <section className="identity" aria-label="Student identity">
      {editing ? (
        <form className="identity-form" onSubmit={handleSubmit}>
          <label htmlFor="student-id">Birth registration number</label>
          <div className="identity-row">
            <input
              id="student-id"
              name="student-id"
              inputMode="numeric"
              autoComplete="off"
              maxLength={17}
              placeholder="17 digits"
              value={draft}
              aria-invalid={identity.validationError ? true : undefined}
              aria-describedby={
                identity.validationError ? 'student-id-error' : undefined
              }
              onChange={(event) =>
                setDraft(event.target.value.replace(/\D/g, ''))
              }
            />
            <button type="submit" className="btn btn-primary">
              Save
            </button>
            {identity.studentId ? (
              <button
                type="button"
                className="btn btn-ghost"
                onClick={() => {
                  setDraft(identity.studentId);
                  setEditing(false);
                }}
              >
                Cancel
              </button>
            ) : null}
          </div>
          {identity.validationError ? (
            <p id="student-id-error" className="error-text" role="alert">
              {identity.validationError}
            </p>
          ) : (
            <p className="hint">
              No login needed. This number identifies your applications on this
              device.
            </p>
          )}
        </form>
      ) : (
        <div className="identity-row">
          <span className="identity-label">Student</span>
          <strong className="identity-value" data-testid="student-id-badge">
            {identity.studentId}
          </strong>
          <button
            type="button"
            className="btn btn-ghost"
            onClick={() => setEditing(true)}
          >
            Change
          </button>
          <button type="button" className="btn btn-ghost" onClick={identity.clear}>
            Clear
          </button>
        </div>
      )}
    </section>
  );
}
