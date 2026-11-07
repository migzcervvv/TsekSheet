import type { ReactNode } from 'react';

export type Activity = 'overview' | 'receipts' | 'worksheets' | 'settings' | 'legacy';
export function Navigation({ activity, onSelect, active = true }: { activity: Activity; active?: boolean; onSelect: (activity: Activity) => void }) {
  const items: { id: Activity; label: string; detail: string }[] = [
    {id:'overview',label:'Overview',detail:'Workbook status'},
    {id:'receipts',label:'Process Receipts',detail:'Queue, review & add rows'},
    {id:'worksheets',label:'Read-only inspection',detail:'Secondary workbook tool'},
    {id:'settings',label:'Settings',detail:'Appearance & troubleshooting'},
  ];
  const button = (item:typeof items[number]) => <button disabled={!active&&item.id==='overview'} aria-label={item.label} aria-description={item.detail} data-activity={item.id} key={item.id} aria-current={activity === item.id ? 'page' : undefined} onClick={() => onSelect(item.id)}><span>{item.label}</span><small>{item.detail}</small></button>;
  const secondary = (item:typeof items[number]) => item.id==='worksheets';
  return <nav aria-label="Workspace navigation">{items.filter(item=>!secondary(item)).map(button)}<details className="secondary-tools" open={activity==='worksheets'||activity==='legacy'}><summary>Secondary tools</summary>{items.filter(secondary).map(button)}</details></nav>;
}
export function StatusBadge({ children, tone = 'neutral' }: { children: ReactNode; tone?: 'neutral' | 'success' | 'warning' | 'error' | 'unavailable' }) {
  return <span className={`status-badge ${tone}`}>{children}</span>;
}
export function EmptyState({ title, children }: { title: string; children: ReactNode }) {
  return <div className="empty-state"><h2>{title}</h2><p>{children}</p></div>;
}

export type MessageKind = 'information' | 'success' | 'warning' | 'error' | 'unavailable';
export function Notice({kind, title, children}: {kind:MessageKind; title:string; children?:ReactNode}) {
 const symbols={information:'i',success:'+',warning:'!',error:'×',unavailable:'—'};
 return <div className={'semantic-notice '+kind} role={kind==='error'?'alert':'note'}><span className="notice-symbol" aria-hidden="true">{symbols[kind]}</span><div><strong>{title}</strong>{children && <div className="notice-description">{children}</div>}</div></div>;
}
