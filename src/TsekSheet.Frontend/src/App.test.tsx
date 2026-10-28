import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import App from './App';

afterEach(cleanup);

test('renders the application welcome message and frontend status', () => {
  render(<App />);

  expect(
    screen.getByRole('heading', { level: 1, name: 'Welcome to TsekSheet' }).textContent,
  ).toBe('Welcome to TsekSheet');
  expect(screen.getByText('React frontend is running.').textContent).toBe(
    'React frontend is running.',
  );
});
