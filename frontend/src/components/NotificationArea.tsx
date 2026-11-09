import { useNotifications } from '../notifications';

export function NotificationArea() {
  const { notifications, dismiss } = useNotifications();

  return (
    <div className="toast-area" aria-live="polite" aria-atomic="false">
      {notifications.map((notification) => (
        <div
          key={notification.id}
          className={`toast toast-${notification.kind}`}
          role={notification.kind === 'error' ? 'alert' : 'status'}
        >
          <span>{notification.message}</span>
          <button
            type="button"
            className="toast-close"
            aria-label="Dismiss notification"
            onClick={() => dismiss(notification.id)}
          >
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
