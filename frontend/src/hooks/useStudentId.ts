import { useCallback, useState } from 'react';

export const STUDENT_ID_STORAGE_KEY = 'sas.studentId';

const STUDENT_ID_PATTERN = /^\d{17}$/;

export interface StudentIdentity {
  studentId: string;
  validationError: string | null;
  save: (value: string) => boolean;
  clear: () => void;
}

function readStoredStudentId(): string {
  try {
    return localStorage.getItem(STUDENT_ID_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

export function useStudentId(): StudentIdentity {
  const [studentId, setStudentId] = useState<string>(readStoredStudentId);
  const [validationError, setValidationError] = useState<string | null>(null);

  const save = useCallback((value: string): boolean => {
    const trimmed = value.trim();

    if (!STUDENT_ID_PATTERN.test(trimmed)) {
      setValidationError('Enter a valid 17-digit birth registration number.');
      return false;
    }

    try {
      localStorage.setItem(STUDENT_ID_STORAGE_KEY, trimmed);
    } catch {
      // Storage can be unavailable (private mode); the in-memory identity still works.
    }

    setStudentId(trimmed);
    setValidationError(null);
    return true;
  }, []);

  const clear = useCallback(() => {
    try {
      localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
    } catch {
      // Ignore storage failures.
    }

    setStudentId('');
    setValidationError(null);
  }, []);

  return { studentId, validationError, save, clear };
}
