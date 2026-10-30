import {useState} from 'react';

// The action is captured, never run until the user explicitly chooses discard.
export function useReviewDiscard(count: number, uncertain = false) {
  const [action, setAction] = useState<(() => void) | null>(null);
  const guard = (next: () => void) => { if (count > 0 || uncertain) setAction(() => next); else next(); };
  const dialog = action && <div role="alertdialog" aria-modal="true" aria-labelledby="review-discard-title" className="discard-warning review-discard" onKeyDown={e => {
    if(e.key === 'Escape') setAction(null);
    if(e.key === 'Tab') {e.preventDefault(); const buttons=e.currentTarget.querySelectorAll('button'); (document.activeElement===buttons[0]?buttons[1]:buttons[0]).focus();}
  }}>
    <strong id="review-discard-title">Discard review decisions?</strong>
    <p>{uncertain ? 'Up to ' : ''}{count} in-memory review {count === 1 ? 'decision' : 'decisions'}. Closing or replacing this review permanently discards them.{uncertain && ' A recent review operation outcome is unconfirmed.'}</p>
    <div className="actions"><button autoFocus onClick={() => setAction(null)}>Continue Reviewing</button><button onClick={() => {setAction(null); action();}}>Discard &amp; Close</button></div>
  </div>;
  return {guard, dialog, confirming: action !== null};
}
