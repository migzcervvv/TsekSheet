import { useEffect, useRef, useState } from 'react';
import { BridgeClient, nativeTransport } from './bridge/client';
import type { Info } from './bridge/client';
import WorkbookViewer from './WorkbookViewer';

export default function App() {
  const client = useRef<BridgeClient | null>(null);
  const operation = useRef<string | null>(null);
  const [connection, setConnection] = useState('Connecting');
  const [info, setInfo] = useState<Info | null>(null);
  const [error, setError] = useState('');
  const [runState, setRunState] = useState('Not started');
  const [progress, setProgress] = useState(0);
  const [file, setFile] = useState('No file selected');
  const [selecting, setSelecting] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [workbooksAvailable, setWorkbooksAvailable] = useState(false);
  const connected = connection === 'Connected';
  const running = runState === 'Running' || runState === 'Cancelling';

  useEffect(() => {
    let active = true;
    const transport = nativeTransport();
    if (!transport) { setConnection('Desktop host unavailable'); return; }
    const bridge = new BridgeClient(transport);
    client.current = bridge;
    setConnection('Connecting'); setError(''); setWorkbooksAvailable(false);
    bridge.onProgress = p => { if (active && operation.current === p.operationId) setProgress(Math.round(p.completed / p.total * 100)); };
    void bridge.request('foundation.handshake', { supportedVersions: [1] }).promise
      .then(handshake => { if (active) setWorkbooksAvailable(handshake.capabilities.workbooks); return bridge.request('foundation.getInfo', {}).promise; })
      .then(metadata => { if (active) { setInfo(metadata); setConnection('Connected'); } })
      .catch(e => { if (active) { setConnection('Connection failed'); setError(String(e.message)); } });
    const close = () => bridge.dispose();
    window.addEventListener('pagehide', close);
    return () => { active = false; window.removeEventListener('pagehide', close); bridge.dispose(); client.current = null; };
  }, [attempt]);

  async function run() {
    if (!client.current || !connected) return;
    setError(''); setProgress(0); setRunState('Running');
    const request = client.current.request('diagnostics.run', { steps: 50, delayMs: 100 });
    operation.current = request.id;
    try { const result = await request.promise; setRunState(result.status === 'completed' ? 'Completed' : 'Cancelled'); operation.current = null; }
    catch (e) { setRunState('Failed'); setError((e as Error).message); }
  }
  async function cancel() {
    if (!client.current || !operation.current) return;
    const outcomeUnknown = runState === 'Failed';
    setRunState('Cancelling');
    try {
      const result = await client.current.request('diagnostics.cancel', { operationId: operation.current }).promise;
      if (outcomeUnknown) { setRunState(result.status === 'notRunning' ? 'Not running' : 'Cancellation requested'); operation.current = null; }
    }
    catch (e) { setError((e as Error).message); setRunState('Failed'); }
  }
  async function select() {
    if (!client.current) return;
    setSelecting(true); setError('');
    try { const result = await client.current.request('foundation.selectFile', {}).promise; setFile(result.status === 'selected' ? result.displayName! : 'Selection cancelled'); }
    catch (e) { setError((e as Error).message); }
    finally { setSelecting(false); }
  }

  return (
    <div className="shell">
      <header className="topbar"><span className="wordmark">TsekSheet</span><span>Desktop foundation</span><span className="local">Local application</span></header>
      <main>
        <div className="heading"><div><h1>Welcome to TsekSheet</h1><p>React frontend is running.</p></div><span className="version">{info ? `Version ${info.version}` : 'Starting up'}</span></div>
        <section className="connection" aria-labelledby="connection-title">
          <div><h2 id="connection-title">Desktop connection</h2><p>React interface ↔ C# application</p></div>
          <strong role="status" className={connected ? 'connected' : ''}>{connection}</strong>
          {connection === 'Connection failed' && <button onClick={() => setAttempt(a => a + 1)}>Retry connection</button>}
        </section>
        {!nativeTransport() && <p className="notice">Open the Windows desktop application to use native controls. Browser preview has no C# connection.</p>}
        {error && <p role="alert" className="error">{error}</p>}
        <WorkbookViewer bridge={connected ? client.current : null} available={workbooksAvailable} />
        <div className="workspace">
          <section className="panel" aria-labelledby="diagnostics-title">
            <div className="panel-heading"><h2 id="diagnostics-title">Connection diagnostic</h2><span>{runState}</span></div>
            <p>Run a short operation in C# to check progress reporting and cancellation.</p>
            <div className="progress-label"><label htmlFor="diagnostic-progress">Diagnostic progress</label><span>{progress}%</span></div>
            <progress id="diagnostic-progress" value={progress} max="100" />
            <div className="actions"><button className="primary" disabled={!connected || running} onClick={() => void run()}>Run diagnostic</button><button disabled={!connected || !operation.current || runState === 'Cancelling'} onClick={() => void cancel()}>Cancel</button></div>
            <p className="detail">Diagnostic data stays in memory.</p>
          </section>
          <section className="panel" aria-labelledby="selection-title">
            <div className="panel-heading"><h2 id="selection-title">Native file selection</h2><span>Demonstration</span></div>
            <p>Choose a file with the Windows dialog. The selected file is not opened, read, or edited.</p>
            <div className="selection" aria-live="polite">{file}</div>
            <button disabled={!connected || selecting} onClick={() => void select()}>{selecting ? 'Dialog open…' : 'Choose a file'}</button>
            <p className="detail">Only its display name reaches this interface.</p>
          </section>
        </div>
        <section className="scope" aria-labelledby="scope-title"><h2 id="scope-title">Available in a later phase</h2><p>Worksheet replacement, new-sheet creation, existing-file saving, templates, and OCR are not available. Supported cell edits can be merged in memory and exported with Save As to a new file.</p></section>
      </main>
      <footer><span>Working copies stay in memory · Saving unavailable</span><span>Windows x64 · Offline-first</span></footer>
    </div>
  );
}
