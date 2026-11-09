import { BrowserRouter, NavLink, Route, Routes } from 'react-router-dom';
import { ErrorBoundary } from './components/ErrorBoundary';
import { IdentityBar } from './components/IdentityBar';
import { NotificationArea } from './components/NotificationArea';
import { useStudentId } from './hooks/useStudentId';
import { NotificationsProvider, useNotifications } from './notifications';
import { ClassesPage } from './pages/ClassesPage';
import { MyHoldsPage } from './pages/MyHoldsPage';

function NotFound() {
  return (
    <div className="empty-state">
      <h2>Page not found</h2>
      <p>
        <NavLink to="/">Return to the classes list</NavLink>.
      </p>
    </div>
  );
}

function AppShell() {
  const identity = useStudentId();
  const { notify } = useNotifications();

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand">
          <h1>School Admission System</h1>
          <p className="muted">
            First come, first served. Hold a seat, then pay the fee before it expires.
          </p>
        </div>
        <IdentityBar identity={identity} />
      </header>

      <nav className="tabs" aria-label="Sections">
        <NavLink
          to="/"
          end
          className={({ isActive }) => (isActive ? 'tab active' : 'tab')}
        >
          Classes
        </NavLink>
        <NavLink
          to="/my-holds"
          className={({ isActive }) => (isActive ? 'tab active' : 'tab')}
        >
          My Applications
        </NavLink>
      </nav>

      <main className="content">
        <ErrorBoundary>
          <Routes>
            <Route
              path="/"
              element={<ClassesPage studentId={identity.studentId} notify={notify} />}
            />
            <Route
              path="/my-holds"
              element={<MyHoldsPage studentId={identity.studentId} notify={notify} />}
            />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </ErrorBoundary>
      </main>

      <NotificationArea />
    </div>
  );
}

export default function App() {
  return (
    <NotificationsProvider>
      <BrowserRouter
        future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
      >
        <AppShell />
      </BrowserRouter>
    </NotificationsProvider>
  );
}
