import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, toUserMessage } from '../api/client';
import { ClassCard } from '../components/ClassCard';
import { CreateClassForm } from '../components/CreateClassForm';
import { ErrorBanner } from '../components/ErrorBanner';
import { useClasses } from '../hooks/useClasses';
import { useMyHolds } from '../hooks/useMyHolds';
import type { Notify } from '../notifications';
import type { ClassInfo } from '../types';

interface ClassesPageProps {
  studentId: string;
  notify: Notify;
}

export function ClassesPage({ studentId, notify }: ClassesPageProps) {
  const classes = useClasses();
  const myHolds = useMyHolds(studentId);
  const navigate = useNavigate();
  const [applying, setApplying] = useState<string | null>(null);

  const activeClassIds = useMemo(
    () =>
      new Set(
        (myHolds.data ?? [])
          .filter((hold) => hold.status === 'HELD' || hold.status === 'CONFIRMED')
          .map((hold) => hold.classId),
      ),
    [myHolds.data],
  );

  const networkError = classes.error ?? (studentId ? myHolds.error : null);

  const handleCreate = async (name: string, totalSeats: number) => {
    const created = await api.createClass(name, totalSeats);
    notify('success', `Class "${created.name}" created.`);
    await classes.refresh();
  };

  const handleApply = async (classId: string) => {
    if (!studentId) {
      notify('error', 'Enter your birth registration number first.');
      return;
    }

    setApplying(classId);

    try {
      await api.apply(classId, studentId);
      notify(
        'success',
        'Application created. Pay the admission fee before the hold expires.',
      );
      await Promise.all([classes.refresh(), myHolds.refresh()]);
      navigate('/my-holds');
    } catch (cause) {
      notify('error', toUserMessage(cause));
      await Promise.all([classes.refresh(), myHolds.refresh()]);
    } finally {
      setApplying(null);
    }
  };

  const reasonFor = (classInfo: ClassInfo): string | null => {
    if (!studentId) {
      return 'Enter your birth registration number first.';
    }

    if (classInfo.availableSeats <= 0) {
      return 'No seats available.';
    }

    if (activeClassIds.has(classInfo.id)) {
      return 'You already have an active application in this class.';
    }

    return null;
  };

  const retry = () => {
    void classes.refresh();
    void myHolds.refresh();
  };

  return (
    <div className="stack">
      <section className="panel">
        <h2>School admin</h2>
        <p className="muted">
          Create a class. Each shift is a separate class with its own seats.
        </p>
        <CreateClassForm onCreate={handleCreate} />
      </section>

      {networkError ? (
        <ErrorBanner message={networkError} onRetry={retry} />
      ) : null}

      <section>
        <div className="section-header">
          <h2>Classes</h2>
          <span className="muted">Seat counts refresh every 3 seconds</span>
        </div>

        {classes.loading && !classes.data ? (
          <p className="muted">Loading classes…</p>
        ) : null}

        {!classes.loading && (classes.data?.length ?? 0) === 0 ? (
          <div className="empty-state">
            No classes yet. Create the first one above.
          </div>
        ) : null}

        <div className="card-grid">
          {(classes.data ?? []).map((classInfo) => (
            <ClassCard
              key={classInfo.id}
              classInfo={classInfo}
              disabledReason={reasonFor(classInfo)}
              pending={applying === classInfo.id}
              onApply={handleApply}
            />
          ))}
        </div>
      </section>
    </div>
  );
}
