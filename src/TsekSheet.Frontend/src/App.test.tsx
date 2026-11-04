import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import App from './App';

afterEach(cleanup);

test('starts in the receipt task and preserves shell navigation', () => {
  render(<App />);

  expect(
    screen.getByRole('heading', { level: 1, name: 'Workbook overview' }).textContent,
  ).toBe('Workbook overview');
  expect(screen.getByText('Your active workbook and next steps.').textContent).toBe(
    'Your active workbook and next steps.',
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

test('receipt and Excel entry stay available without a legacy workbook project', () => {
 const {container}=render(<App/>);
 expect((screen.getByRole('button',{name:'Receipts'}) as HTMLButtonElement).disabled).toBe(false);
 expect(screen.queryByRole('button',{name:'Placement'})).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Settings'}));
 expect(screen.getByRole('heading',{level:1}).textContent).toBe('Settings');
 fireEvent.change(screen.getByLabelText('Appearance'),{target:{value:'dark'}});
 expect(container.querySelector('.shell')?.getAttribute('data-theme')).toBe('dark');
 expect(screen.getByText('Troubleshooting')).toBeDefined();
});
