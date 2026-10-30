import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach,expect,test,vi} from 'vitest';
import SectionReview from './SectionReview';
import {BridgeClient} from './bridge/client';
import type {Transport} from './bridge/client';
import {validSectionReview} from './bridge/sectionReview';
import type {ReviewDecision} from './bridge/sectionReview';
import {reviewBook as book,reviewFixture as fixture} from './reviewTestFixture';
const sid=book.sessionId,wid=book.worksheets[0].id;
const clients:BridgeClient[]=[];afterEach(()=>{cleanup();clients.forEach(c=>c.dispose());clients.length=0;});
function setup(mode='normal'){
 let snapshot=fixture();if(mode==='guided')snapshot.originalRecordRows.push(20);const messages:{command:string;payload:Record<string,unknown>}[]=[];const listeners=new Set<(e:{data:unknown})=>void>();
 const host:Transport={addEventListener:(_,l)=>{listeners.add(l);},removeEventListener:(_,l)=>{listeners.delete(l);},postMessage:m=>{const req=m as {command:string;requestId:string;payload:Record<string,unknown>};messages.push(req);queueMicrotask(()=>{
  let result:unknown={status:'cancelled'},error:unknown;
  if(req.command==='sectionReview.start') {if(mode==='missing')error={code:'PROFILE_REQUIRED',message:'Section review requires a configured profile.'};else result={status:'started',review:snapshot};}
  if(req.command==='sectionReview.decide') {snapshot=structuredClone(snapshot);snapshot.binding.reviewRevision++;snapshot.decisions.push(req.payload.decision as ReviewDecision);snapshot.after.uncertainIdentities=0;snapshot.identityQueue[0].vendorStatus='Reviewer-confirmed identity';if(mode==='stale')snapshot.binding.sourceSha256='D'.repeat(64);snapshot.report.sourceSha256=snapshot.binding.sourceSha256;result=snapshot;}
  if(req.command==='sectionReview.retract'){snapshot=structuredClone(snapshot);snapshot.binding.reviewRevision++;snapshot.decisions=[];snapshot.after.uncertainIdentities=1;snapshot.identityQueue[0].vendorStatus='Missing literal vendor identity';result=snapshot;}
  listeners.forEach(l=>l({data:{protocolVersion:1,type:'response',requestId:req.requestId,ok:!error,result,error}}));
 });}};const bridge=new BridgeClient(host);clients.push(bridge);const navigate=vi.fn();const ui=render(<SectionReview bridge={bridge} book={book} sheetId={wid} disabled={false} onNavigate={navigate}/>);return {messages,navigate,ui};
}
async function start(){fireEvent.click(screen.getByRole('button',{name:'Select profile and start review'}));await screen.findByRole('table',{name:'Review coverage'});}

test.each([1,2])('closing with %i decisions preserves on cancellation and discards only explicitly',async count=>{
 const s=setup();await start();fireEvent.click(screen.getByRole('button',{name:'Unassigned Rows'}));
 for(let i=0;i<count;i++){
  fireEvent.change(screen.getByRole('spinbutton',{name:'Review row'}),{target:{value:String(i+4)}});
  fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Artificial investigation '+i}});
  fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));
  await waitFor(()=>expect((screen.getByRole('button',{name:'Close section review'}) as HTMLButtonElement).disabled).toBe(false));
 }
 fireEvent.click(screen.getByRole('button',{name:'Close section review'}));
 const dialog=screen.getByRole('alertdialog');expect(dialog.textContent).toContain(`${count} in-memory review`);
 expect(document.activeElement?.textContent).toBe('Continue Reviewing');
 expect(s.messages.filter(m=>m.command==='sectionReview.cancel')).toHaveLength(0);
 fireEvent.click(screen.getByRole('button',{name:'Continue Reviewing'}));
 expect(screen.getByLabelText('Read-only section review').textContent).toContain('Artificial investigation');
 fireEvent.click(screen.getByRole('button',{name:'Select profile and start review'}));
 expect(s.messages.filter(m=>m.command==='sectionReview.start')).toHaveLength(1);
 fireEvent.keyDown(screen.getByRole('alertdialog'),{key:'Escape'});
 fireEvent.click(screen.getByRole('button',{name:'Close section review'}));
 fireEvent.click(screen.getByRole('button',{name:'Discard & Close'}));
 await waitFor(()=>expect(screen.queryByRole('table')).toBeNull());
 expect(s.messages.filter(m=>m.command==='sectionReview.cancel')).toHaveLength(1);
});

test('zero decisions close without confirmation',async()=>{const s=setup();await start();fireEvent.click(screen.getByRole('button',{name:'Close section review'}));expect(screen.queryByRole('alertdialog')).toBeNull();await waitFor(()=>expect(s.messages.some(m=>m.command==='sectionReview.cancel')).toBe(true));});

test('subtotal guided flow records independent boundary and non-record decisions only after explicit reasons',async()=>{
 const s=setup('guided');await start();fireEvent.click(screen.getByRole('button',{name:'Sections & Subtotals'}));fireEvent.click(screen.getByRole('button',{name:'Boundary north row 20'}));
 fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Artificial section ending checked'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));
 fireEvent.click(await screen.findByRole('button',{name:'Review subtotal / non-record classification'}));
 expect(s.messages.filter(m=>m.command==='sectionReview.decide')).toHaveLength(1);
 expect((screen.getByRole('combobox',{name:'Review decision type'}) as HTMLSelectElement).value).toBe('non-record');
 fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Artificial subtotal classified separately'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));
 fireEvent.click(screen.getByRole('button',{name:'Review History'}));await screen.findByRole('button',{name:'Retract assertion row 20 non-record'});expect(screen.getByRole('button',{name:'Retract assertion row 20 boundary'})).toBeDefined();
 expect(s.messages.filter(m=>m.command==='sectionReview.decide').map(m=>(m.payload.decision as ReviewDecision).kind)).toEqual(['boundary','non-record']);
});
test('configured profile is required and no global private default is loaded',async()=>{const s=setup('missing');expect(s.messages).toHaveLength(0);fireEvent.click(screen.getByRole('button',{name:'Select profile and start review'}));expect((await screen.findByRole('alert')).textContent).toContain('configured profile');expect(screen.queryByRole('table')).toBeNull();});
test('source navigation identity confirmation and retraction carry complete backend binding',async()=>{const s=setup();await start();fireEvent.click(screen.getByRole('button',{name:'Missing Vendors'}));fireEvent.click(screen.getByRole('button',{name:'Review identity row 5'}));expect(s.navigate).toHaveBeenCalledWith(5,1);fireEvent.change(screen.getByRole('spinbutton',{name:'Identity evidence row'}),{target:{value:'4'}});fireEvent.change(screen.getByRole('combobox',{name:'Evidence column'}),{target:{value:'3'}});fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Artificial source evidence explicitly checked'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));fireEvent.click(screen.getByRole('button',{name:'Review History'}));await screen.findByRole('button',{name:'Retract assertion row 5 identity'});
 const request=s.messages.find(m=>m.command==='sectionReview.decide')!.payload;expect(request.binding).toEqual(fixture().binding);expect(request.decision).toMatchObject({kind:'identity',row:5,evidenceRow:4,evidenceColumn:3,value:null});fireEvent.click(screen.getByRole('button',{name:'Missing Vendors'}));expect(screen.getByRole('combobox',{name:'Identity case'}).textContent).toContain('Reviewer-confirmed identity');fireEvent.click(screen.getByRole('button',{name:'Review History'}));fireEvent.click(screen.getByRole('button',{name:'Retract assertion row 5 identity'}));fireEvent.click(screen.getByRole('button',{name:'Missing Vendors'}));await waitFor(()=>expect(screen.getByRole('combobox',{name:'Identity case'}).textContent).toContain('Missing literal vendor identity'));expect(s.messages.find(m=>m.command==='sectionReview.retract')!.payload.binding).toMatchObject({reviewRevision:1});});
test('boundary and duplicate review use source rows and retain warnings without insertion authority',async()=>{const s=setup();await start();fireEvent.click(screen.getByRole('button',{name:'Sections & Subtotals'}));fireEvent.click(screen.getByRole('button',{name:'Boundary north row 20'}));expect(s.navigate).toHaveBeenCalledWith(20,1);expect((screen.getByRole('combobox',{name:'Review decision type'}) as HTMLSelectElement).value).toBe('boundary');fireEvent.click(screen.getByRole('button',{name:'Potential Duplicates'}));fireEvent.click(screen.getByRole('button',{name:'Inspect duplicate row 4 · section north'}));fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Warning inspected only'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));fireEvent.click(screen.getByRole('button',{name:'Review History'}));await screen.findByRole('button',{name:'Retract assertion row 4 duplicate'});fireEvent.click(screen.getByRole('button',{name:'Potential Duplicates'}));expect(s.messages.find(m=>m.command==='sectionReview.decide')!.payload.decision).toMatchObject({kind:'duplicate',value:'C'.repeat(64)});expect(screen.getByRole('combobox',{name:'Duplicate warning'})).toBeDefined();fireEvent.click(screen.getByRole('button',{name:'Overview'}));expect(screen.getByText(/Structural insertion NOT authorized/)).toBeDefined();});
test('stale responses cannot replace current review and closing clears rendered accounting state',async()=>{setup('stale');await start();fireEvent.click(screen.getByRole('button',{name:'Missing Vendors'}));fireEvent.click(screen.getByRole('button',{name:'Review identity row 5'}));fireEvent.change(screen.getByRole('textbox',{name:'Review reason'}),{target:{value:'Synthetic reason'}});fireEvent.click(screen.getByRole('button',{name:'Confirm read-only assertion'}));expect((await screen.findByRole('alert')).textContent).toContain('identity changed');expect(screen.getByRole('combobox',{name:'Identity case'}).textContent).toContain('Missing literal');fireEvent.click(screen.getByRole('button',{name:'Close section review'}));expect(screen.getByRole('alertdialog').textContent).toContain('outcome is unconfirmed');fireEvent.click(screen.getByRole('button',{name:'Discard & Close'}));await waitFor(()=>expect(screen.queryByRole('table')).toBeNull());});
test('unmount cancels private review and response validator rejects unsafe authority or over-budget state',async()=>{const s=setup();await start();s.ui.unmount();expect(s.messages.at(-1)).toMatchObject({command:'sectionReview.cancel',payload:{sessionId:sid,worksheetId:wid}});expect(validSectionReview(fixture())).toBe(true);const unsafe=fixture();Object.assign(unsafe,{canInsert:true});expect(validSectionReview(unsafe)).toBe(false);const over=fixture();over.report.coverage.accountedBytes=16777217;expect(validSectionReview(over)).toBe(false);const incomplete=fixture();incomplete.duplicateKeys=[];expect(validSectionReview(incomplete)).toBe(false);});
