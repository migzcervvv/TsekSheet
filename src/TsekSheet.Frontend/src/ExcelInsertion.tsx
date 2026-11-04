import { useEffect, useRef, useState } from 'react';
import type { BridgeClient } from './bridge/client';
import type { ExcelCatalog, ExcelChange, ExcelPreview } from './bridge/excel';
import type { ReceiptState } from './bridge/receipts';
import { Notice } from './ui/Workspace';

function columnLabel(column:number){let label='';while(column>0){column--;label=String.fromCharCode(65+column%26)+label;column=Math.floor(column/26);}return label;}

export default function ExcelInsertion({bridge,review,dirty,disabled}:{bridge:BridgeClient;review:ReceiptState|null;dirty:boolean;disabled:boolean}) {
  const [catalog,setCatalog]=useState<ExcelCatalog>({workbooks:[],sections:[],changes:[]});
  const [workbook,setWorkbook]=useState(''),[section,setSection]=useState('');
  const [sheet,setSheet]=useState('');
  const [preview,setPreview]=useState<ExcelPreview|null>(null);
  const [busy,setBusy]=useState(false),[error,setError]=useState('');
  const lock=useRef(false);
  const binding=JSON.stringify(review?.version);
  useEffect(()=>{setPreview(null);},[binding,dirty]);
  const book=catalog.workbooks.find(w=>w.id===workbook);
  const sheets=book?.sheets.filter(name=>catalog.sections.some(s=>!s.sheetName||s.sheetName===name))??[];
  const selectedSheet=sheet|| (sheets.length===1?sheets[0]:'');
  const sections=catalog.sections.filter(s=>selectedSheet&&(!s.sheetName||s.sheetName===selectedSheet));
  const blocked=disabled||busy||dirty||review?.status!=='Active'||!review.version||review.fields.some(f=>f.value.trim()!==''&&f.status!=='Verified');
  async function run(action:()=>Promise<void>) {
    if(lock.current)return;
    lock.current=true;setBusy(true);setError('');
    try{await action();}catch(e){setPreview(null);setError((e as Error).message);}finally{lock.current=false;setBusy(false);}
  }
  useEffect(()=>{setCatalog(c=>({...c,sections:[]}));setSection('');setSheet('');},[review?.version?.profileId]);
  function refresh(open:boolean){void run(async()=>{const current=await bridge.request('excel.listWorkbooks',{open}).promise;setCatalog(current);setPreview(null);if(!current.workbooks.some(w=>w.id===workbook)){setWorkbook(current.workbooks[0]?.id??'');setSection('');setSheet('');}});}
  return <section className="excel-insertion" aria-label="Insert verified receipt into Excel" aria-busy={busy}>
    <div className="reader-heading"><div><h3>Insert into Excel</h3><p>Preview one verified row. Review highlighted changes in Excel, then save there.</p></div></div>
    <div className="actions"><button disabled={disabled||busy} onClick={()=>refresh(true)}>Open workbook in Excel</button><button disabled={disabled||busy} onClick={()=>refresh(false)}>Refresh Excel workbooks</button></div>
    {error&&<Notice kind="error" title="Excel action failed">{error} Check Excel before submitting another insert.</Notice>}
    {catalog.workbooks.length===0?<p>No workbook selected in the TsekSheet Excel instance.</p>:<div className="excel-destination actions">
      <label>Excel workbook<select aria-label="Excel workbook" value={workbook} disabled={busy||disabled} onChange={e=>{setWorkbook(e.target.value);setSheet('');setSection('');setPreview(null);}}><option value="">Choose workbook</option>{catalog.workbooks.map(w=><option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
      <label>Excel sheet<select aria-label="Excel sheet" value={selectedSheet} disabled={busy||disabled||!book} onChange={e=>{setSheet(e.target.value);setSection('');setPreview(null);}}><option value="">Choose sheet</option>{sheets.map(name=><option key={name}>{name}</option>)}</select></label>
      <label>Excel section<select aria-label="Excel section" value={section} disabled={busy||disabled||!selectedSheet} onChange={e=>{setSection(e.target.value);setPreview(null);}}><option value="">Choose section</option>{sections.map(s=><option key={s.id} value={s.id}>{s.label}{s.sheetName?' · '+s.sheetName:''}</option>)}</select></label>
      <button disabled={blocked||!workbook||!selectedSheet||!section} onClick={()=>void run(async()=>{const result=await bridge.request('excel.previewInsert',{expected:review!.version!,workbookId:workbook,sectionId:section,sheet:selectedSheet}).promise;setPreview(result);})}>Preview Excel insert</button>
    </div>}
    {book&&catalog.sections.length>0&&sheets.length===0&&<p role="status">No sheets match this profile layout.</p>}
    {dirty&&<p role="status">Save and verify the edited fields before previewing.</p>}
    {preview&&<div aria-label="Excel insertion preview"><h4>{book?.name} · {preview.sheet}!{preview.range}</h4><div className="mapped-values"><table><caption>Values to insert</caption><thead><tr><th scope="col">Field</th><th scope="col">Column</th><th scope="col">Value</th></tr></thead><tbody>{preview.values.map(v=><tr key={v.key}><th scope="row">{v.header}</th><td>{columnLabel(v.column)}</td><td data-numeric={typeof v.value==='number'}>{v.displayValue!==undefined&&v.displayValue!==null?v.displayValue:v.value===null?'—':String(v.value)}</td></tr>)}</tbody></table></div>
      {preview.manualReview.map(w=><Notice key={w.cell} kind="warning" title={`Subtotal ${w.cell} needs review`}>{w.reason}</Notice>)}
      <button className="primary" disabled={blocked} onClick={()=>void run(async()=>{const result=await bridge.request('excel.insert',{expected:review!.version!,previewToken:preview.token}).promise;setCatalog(c=>({...c,changes:[...c.changes,result]}));setPreview(null);})}>Insert verified row</button>
    </div>}
    {catalog.changes.length>0&&<div aria-label="Inserted Excel rows"><h4>Inserted rows</h4><p>Changes are highlighted in Excel and remain unsaved until you save.</p><ul>{catalog.changes.map((change:ExcelChange,i)=><li key={i}>{change.workbook} · {change.sheet}!{change.range}{change.manualReview.map(w=><p key={w.cell}>Review {w.cell}: {w.reason}</p>)}</li>)}</ul></div>}
  </section>;
}
