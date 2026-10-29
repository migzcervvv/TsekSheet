import { cleanup, fireEvent, render, screen } from '@testing-library/react';
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

test('header keeps its text fallback when the local logo cannot load', () => {
  const { container } = render(<App />);
  const logo = container.querySelector('header img')!;
  expect(logo.getAttribute('src')).toBe('./branding/TsekSheet-No-BG.svg');
  fireEvent.error(logo);
  expect(container.querySelector('header img')).toBeNull();
  expect(screen.getByText('TsekSheet')).toBeDefined();
});
