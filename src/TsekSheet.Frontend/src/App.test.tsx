import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, test } from 'vitest';
import App from './App';

afterEach(cleanup);

test('starts in the receipt task and preserves shell navigation', () => {
  render(<App />);

  expect(
    screen.getByRole('heading', { level: 1, name: 'Process Receipts' }).textContent,
  ).toBe('Process Receipts');
  expect(screen.getByText('Review receipts and confirm rows in your active workbook.').textContent).toBe(
    'Review receipts and confirm rows in your active workbook.',
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
 expect((screen.getByRole('button',{name:'Process Receipts'}) as HTMLButtonElement).disabled).toBe(false);
 expect((container.querySelector('.secondary-tools') as HTMLDetailsElement).open).toBe(false);
 fireEvent.click(screen.getByText('Secondary tools'));
 expect((screen.getByRole('button',{name:'Read-only inspection'}) as HTMLButtonElement).disabled).toBe(false);
 expect(screen.queryByRole('button',{name:'Legacy receipt review'})).toBeNull();
 expect(screen.queryByRole('button',{name:'Placement'})).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Settings'}));
 expect(screen.getByRole('heading',{level:1}).textContent).toBe('Settings');
 fireEvent.change(screen.getByLabelText('Appearance'),{target:{value:'dark'}});
 expect(container.querySelector('.shell')?.getAttribute('data-theme')).toBe('dark');
 expect(screen.getByText('Troubleshooting')).toBeDefined();
});
