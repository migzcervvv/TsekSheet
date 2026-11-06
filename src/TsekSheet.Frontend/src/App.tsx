import { useEffect, useRef, useState } from 'react';
import { BridgeClient, nativeTransport } from './bridge/client';
import type { Info } from './bridge/client';
import { Navigation, StatusBadge } from './ui/Workspace';
import type { Activity } from './ui/Workspace';
import WorkbookViewer from './WorkbookViewer';
import FastReceiptLoop from './FastReceiptLoop';
import type { ExcelWorkbook } from './bridge/excel';
import type { WorkflowProfile } from './bridge/workflows';

export default function App() {
  const client = useRef<BridgeClient | null>(null);
  const workspaceScroll = useRef<HTMLElement>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const operation = useRef<string | null>(null);
  const [activity, setActivity] = useState<Activity>('receipts');
  const [project, setProject] = useState<ExcelWorkbook|null>(null);
  const [opening,setOpening]=useState(false);
  const openingLock=useRef(false);
  const [protectedWork,setProtectedWork]=useState(false);
  const [legacyProtected,setLegacyProtected]=useState(false);
  const [profiles,setProfiles]=useState<WorkflowProfile[]>([]);
  const [menuOpen, setMenuOpen] = useState(false);
  const [theme, setTheme] = useState('system');
  const [connection, setConnection] = useState('Connecting');
  const [info, setInfo] = useState<Info | null>(null);
  const [error, setError] = useState('');
  const [workbookError,setWorkbookError]=useState('');
  const [runState, setRunState] = useState('Not started');
  const [progress, setProgress] = useState(0);
  const [file, setFile] = useState('No file selected');
  const [selecting, setSelecting] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [workbooksAvailable, setWorkbooksAvailable] = useState(false);
  const [renderingAvailable,setRenderingAvailable]=useState(false);
  const [receiptsAvailable, setReceiptsAvailable] = useState(false);
  const [logoAvailable, setLogoAvailable] = useState(true);
  const connected = connection === 'Connected';
  const running = runState === 'Running' || runState === 'Cancelling';

  useEffect(() => { workspaceScroll.current?.scrollTo?.(0,0); }, [activity]);

  useEffect(() => {
    let active = true;
    const transport = nativeTransport();
    if (!transport) { setConnection('Desktop host unavailable'); return; }
    const bridge = new BridgeClient(transport);
    client.current = bridge;
    setConnection('Connecting'); setError(''); setWorkbooksAvailable(false);
    bridge.onProgress = p => { if (active && operation.current === p.operationId) setProgress(Math.round(p.completed / p.total * 100)); };
    void bridge.request('foundation.handshake', { supportedVersions: [1] }).promise
      .then(handshake => { if (active) {setWorkbooksAvailable(handshake.capabilities.workbooks);setReceiptsAvailable(handshake.capabilities.receipts===true);setRenderingAvailable(handshake.capabilities.sourceRendering===true);} return bridge.request('foundation.getInfo', {}).promise; })
      .then(metadata => { if (active) { setInfo(metadata); setConnection('Connected'); void bridge.request('workflow.profiles',{}).promise.then(p=>{if(active)setProfiles(p.profiles);}).catch(()=>{if(active)setError('Receipt profiles could not load. Retry the connection in Settings.');}); } })
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

  async function openWorkbook(){
    if(!client.current||openingLock.current)return;
    setWorkbookError('');
    if(protectedWork||legacyProtected){setWorkbookError('Workbook switching is blocked while receipts, drafts or destination reviews remain in this session. Your work is retained. Finish the current work and save in Excel before starting a new app session.');return;}
    openingLock.current=true;setOpening(true);
    try{
      const [queue,review]=await Promise.all([client.current.request('receipt.queue',{}).promise,client.current.request('receipt.state',{}).promise]);
      if(queue.items.length||review.source){setWorkbookError('Workbook switching is blocked while receipts remain in this session. Your drafts and destinations are retained. Finish the current work and save in Excel before starting a new app session.');return;}
      const catalog=await client.current.request('excel.listWorkbooks',{open:true}).promise;
      if(catalog.openedWorkbookId===null)return;
      const opened=catalog.workbooks.find(w=>w.id===catalog.openedWorkbookId);
      if(!opened)throw new Error('Excel did not identify the selected workbook. Try opening it again.');
      setProject(opened);setActivity('receipts');setMenuOpen(false);
    }catch{setWorkbookError('Could not open or switch the workbook. Your current workspace is retained. Check that Excel is ready and the file is available, then retry. Receipts or drafts in this session must be finished before switching.');}
    finally{openingLock.current=false;setOpening(false);}
  }
  const titles = {overview:['Workbook overview','Your active workbook and next steps.'],receipts:['Process Receipts','Review receipts and confirm rows in your active workbook.'],worksheets:['Read-only workbook inspection','Secondary tool · inspect a separate workbook without changing your active workspace.'],legacy:['Legacy receipt review','Retained single-receipt and placement tools.'],settings:['Settings','Appearance and local troubleshooting.']};
  return <div className="shell" data-theme={theme} onKeyDown={e=>{if(e.key==='Escape' && menuOpen){setMenuOpen(false);menuButton.current?.focus();}}}>
    <a className="skip-link" href="#main-workspace">Skip to workspace</a>
    <aside tabIndex={0} aria-label="Workbook sidebar" className="sidebar" data-open={menuOpen}>
      <header className="brand-header"><span className="wordmark">{logoAvailable && <span className="brand-mark"><img className="brand-logo" src="./branding/TsekSheet-No-BG.svg" alt="" width="28" height="28" onError={()=>setLogoAvailable(false)}/></span>}TsekSheet</span><span className="workspace-label">Workbook workspace</span></header>
      <div className="project-picker"><span className="sidebar-label">Workbook</span><button className="project-select" disabled={!connected||opening} onClick={()=>void openWorkbook()}>{opening?'Opening workbook…':project?'Switch workbook…':'Open workbook'}</button>{project?<div className="active-project" title={project.name}><strong>{project.name}</strong><small>Open in Excel · save in Excel</small></div>:<p>No workbook open</p>}</div>
      <Navigation active={!!project} activity={activity} onSelect={a=>{if(a==='legacy'&&protectedWork){setWorkbookError('The receipt queue still holds drafts and destination bindings. Continue in Process Receipts; legacy review cannot share this queue without risking that work.');return;}setActivity(a);setMenuOpen(false);if(menuOpen)menuButton.current?.focus();}}/>
      <div className="sidebar-bottom"><p>Files stay on this device.<br/>Reviews stay in memory.</p><span className="version">{info?'Version '+info.version:'Starting up'}</span></div>
    </aside>
    <div className="workspace-frame">
      <header className="topbar"><button ref={menuButton} className="menu-toggle" aria-label="Toggle navigation" aria-expanded={menuOpen} onClick={()=>setMenuOpen(o=>!o)}>Menu</button><span className="workbook-breadcrumb" title={project?.name}>{project?.name??'No workbook selected'} <span className="breadcrumb-separator">/</span> {titles[activity][0]}</span><StatusBadge tone={connected?'success':'neutral'}><span role="status">{connection}</span></StatusBadge>{connection==='Connection failed' && <button onClick={()=>setAttempt(a=>a+1)}>Retry connection</button>}</header>
      <main ref={workspaceScroll} id="main-workspace" tabIndex={0} onKeyDown={e=>{if(e.target===e.currentTarget&&(e.key==='Home'||e.key==='End')){e.preventDefault();e.currentTarget.scrollTo({top:e.key==='Home'?0:e.currentTarget.scrollHeight,behavior:'instant'});}}}>
        <div className="heading"><div><h1>{titles[activity][0]}</h1><p>{titles[activity][1]}</p></div><span className="session-label">{project?'Excel-backed workspace':'No workbook open'}</span></div>
        {!nativeTransport() && <p className="notice">Open the Windows desktop application to use native controls. Browser preview has no C# connection.</p>}
        {error && <p role="alert" className="error">{error}</p>}
        {activity==='receipts'&&!project&&<section className="empty-state"><h2>Open workbook</h2><p>Choose the Excel workbook that will receive your receipt rows.</p><button className="primary" disabled={!connected||opening} onClick={()=>void openWorkbook()}>Open workbook</button></section>}
        {workbookError&&<div role="alert" className="error"><p>{workbookError}</p><button disabled={!connected||opening} onClick={()=>void openWorkbook()}>Retry workbook opening</button></div>}
        {project&&connected&&client.current&&profiles.length>0&&<div hidden={activity!=='receipts'}><FastReceiptLoop key={project.id} bridge={client.current} profiles={profiles} activeWorkbook={project} disabled={opening||!receiptsAvailable} renderingAvailable={renderingAvailable} onProtectedChange={setProtectedWork}/></div>}
        {activity==='overview'&&project&&<p>{project.name} is open in Excel. Process Receipts uses this workbook.</p>}
        <div className="project-workspace" inert={opening} hidden={activity!=='worksheets'&&activity!=='legacy'}><WorkbookViewer bridge={connected?client.current:null} available={workbooksAvailable} receiptsAvailable={receiptsAvailable} renderingAvailable={renderingAvailable} activity={activity==='legacy'?'receipts':activity==='worksheets'?'worksheets':'settings'} onActivityChange={a=>setActivity(a==='receipts'?'legacy':a)} onProtectedChange={setLegacyProtected}/></div>
        <div className="diagnostics-workspace" hidden={activity!=='settings'}>
          <label className="theme-control">Appearance<select aria-label="Appearance" value={theme} onChange={e=>setTheme(e.target.value)}><option value="system">System</option><option value="light">Light</option><option value="dark">Dark</option></select></label><details className="troubleshooting"><summary>Troubleshooting</summary><section className="panel" aria-labelledby="diagnostics-title"><div className="panel-heading"><h2 id="diagnostics-title">Connection diagnostic</h2><span>{runState}</span></div><p>Check progress reporting and cancellation through the desktop host.</p><div className="progress-label"><label htmlFor="diagnostic-progress">Diagnostic progress</label><span>{progress}%</span></div><progress id="diagnostic-progress" value={progress} max="100"/><div className="actions"><button className="primary" disabled={!connected||running} onClick={()=>void run()}>Run diagnostic</button><button disabled={!connected||!operation.current||runState==='Cancelling'} onClick={()=>void cancel()}>Cancel</button></div></section>
          <section className="panel" aria-labelledby="selection-title"><div className="panel-heading"><h2 id="selection-title">Native file selection</h2><span>Diagnostic only</span></div><p>The selected file is not opened, read, or edited.</p><div className="selection" aria-live="polite">{file}</div><button disabled={!connected||selecting} onClick={()=>void select()}>{selecting?'Dialog open…':'Choose a file'}</button></section>
        </details></div>
      </main>
      <footer><span>Original files remain unchanged until an authorized save</span><span>Save Excel changes in Excel</span></footer>
    </div>
  </div>;
}
