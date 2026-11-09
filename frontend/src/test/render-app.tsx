import { render } from '@testing-library/react';
import App from '../App';

export function renderApp(path = '/') {
  window.history.pushState({}, '', path);
  return render(<App />);
}
