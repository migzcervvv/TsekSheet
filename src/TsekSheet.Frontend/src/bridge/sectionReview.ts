export interface ReviewBinding { reviewId:string; sessionId:string; worksheetId:string; worksheetOrder:number; captureRevision:number; sourceSha256:string; profileVersion:number; profileSha256:string; reviewRevision:number }
export interface ReviewDecision { id:string; kind:string; row:number; sectionId:string|null; value:string|null; evidenceRow:number|null; evidenceColumn:number|null; reason:string }
export interface ReviewMetrics { observedRows:number; capturedRows:number; allRowsCaptured:boolean; roleColumnsCovered:boolean; headerRows:number; recordShapedRows:number; ownedRecords:number; uncertainIdentities:number; unassignedMeaningfulRows:number; intermediateSubtotals:number; unclassifiedSubtotals:number; summaryRows:number; boundaryConflicts:number; unresolvedDependencies:number }
export interface ReviewSection { id:string; label:string; parentId:string|null; headingRow:number|null; firstDataRow:number|null; subtotalCandidates:number[]; subtotalRow:number|null; boundaryRuleMatched:boolean; status:string; canInsert:false }
export interface ReviewReport {
  sessionId:string; worksheetId:string; revision:number; sourceSha256:string; profileId:string; profileSha256:string;
  headerRows:number[]; fieldColumns:Record<string,number>; sections:ReviewSection[]; summaryRows:number[]; excludedRows:number[]; entityOnlyRows:number[]; unassignedRows:number[];
  records:{row:number; entity:string|null; normalizedEntity:string|null; sectionId:string|null; classification:string; identifiers:Record<string,string>; hidden:boolean; markerConflict:boolean}[];
  potentialDuplicates:{entity:string; identifier:string; rows:number[]; fields:string[]}[];
  similarNames:{firstEntity:string; secondEntity:string; reason:string}[];
  summaryDependencies:{row:number; column:number; formulaStatus:string; localReferences:string[]; complete:false}[];
  subtotalCandidates:{row:number; column:number; formulaStatus:string; configuredBoundary:boolean}[];
  diagnostics:{code:string; message:string; row:number|null; sectionId:string|null}[];
  hierarchyVerified:boolean; identitiesVerified:boolean; canInsert:false;
  coverage:{startRow:number; endRow:number; columns:number[]; rowsCaptured:number; cellsCaptured:number; hiddenRowsIncluded:number; accountedBytes:number; entireWorksheetRowsCaptured:boolean; roleColumnsComplete:boolean; recordCoverageComplete:boolean};
}
export interface ReviewSnapshot { binding:ReviewBinding; profileId:string; worksheetName:string; report:ReviewReport; before:ReviewMetrics; after:ReviewMetrics; decisions:ReviewDecision[];
 identityQueue:{row:number; fields:Record<string,string>; vendorStatus:string; provisionalSection:string|null; hidden:boolean}[]; originalRecordRows:number[]; duplicateKeys:string[];
 configurationReviewed:boolean; recordCoverageReviewed:boolean; vendorIdentitiesReviewed:boolean; hierarchyVerified:boolean; preservationVerified:false; canInsert:false; captureMilliseconds:number; reanalysisMilliseconds:number }

type Obj=Record<string,unknown>;
const obj=(v:unknown):v is Obj=>typeof v==='object'&&v!==null&&!Array.isArray(v);
const str=(v:unknown):v is string=>typeof v==='string'&&v.length<=1024;
const num=(v:unknown,min=0,max=2147483647):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=max;
const row=(v:unknown)=>num(v,1,1048576);
const nullableRow=(v:unknown)=>v===null||row(v);
const nullableText=(v:unknown)=>v===null||str(v);
const guid=(v:unknown)=>typeof v==='string'&&/^[a-f\d]{8}-(?:[a-f\d]{4}-){3}[a-f\d]{12}$/i.test(v);
const hash=(v:unknown)=>typeof v==='string'&&/^[a-f\d]{64}$/i.test(v);
const list=(v:unknown,max:number,test:(v:unknown)=>boolean):v is unknown[]=>Array.isArray(v)&&v.length<=max&&v.every(test);
const dict=(v:unknown,max:number,test:(v:unknown)=>boolean)=>obj(v)&&Object.keys(v).length<=max&&Object.values(v).every(test);
const bools=(v:Obj,keys:string[])=>keys.every(k=>typeof v[k]==='boolean');
function metrics(v:unknown):boolean {return obj(v)&&bools(v,['allRowsCaptured','roleColumnsCovered'])&&['observedRows','capturedRows','headerRows','recordShapedRows','ownedRecords','uncertainIdentities','unassignedMeaningfulRows','intermediateSubtotals','unclassifiedSubtotals','summaryRows','boundaryConflicts','unresolvedDependencies'].every(k=>num(v[k]));}
export function validSectionReview(v:unknown):v is ReviewSnapshot {
 if(!obj(v)||!obj(v.binding)||!obj(v.report))return false;
 const b=v.binding,r=v.report,c=r.coverage;
 return ['reviewId','sessionId','worksheetId'].every(k=>guid(b[k]))&&num(b.worksheetOrder,1,512)&&num(b.captureRevision)&&num(b.reviewRevision,0,4096)&&num(b.profileVersion,1)&&hash(b.sourceSha256)&&hash(b.profileSha256)
 &&str(v.profileId)&&str(v.worksheetName)&&r.sessionId===b.sessionId&&r.worksheetId===b.worksheetId&&r.revision===b.captureRevision&&r.sourceSha256===b.sourceSha256&&hash(r.profileSha256)&&r.profileId===v.profileId
 &&metrics(v.before)&&metrics(v.after)&&bools(v,['configurationReviewed','recordCoverageReviewed','vendorIdentitiesReviewed','hierarchyVerified'])&&v.preservationVerified===false&&v.canInsert===false&&r.canInsert===false&&bools(r,['hierarchyVerified','identitiesVerified'])
 &&['captureMilliseconds','reanalysisMilliseconds'].every(k=>typeof v[k]==='number'&&Number.isFinite(v[k])&&(v[k] as number)>=0)
 &&list(v.decisions,512,d=>obj(d)&&guid(d.id)&&str(d.kind)&&row(d.row)&&nullableText(d.sectionId)&&nullableText(d.value)&&nullableRow(d.evidenceRow)&&(d.evidenceColumn===null||num(d.evidenceColumn,1,16384))&&str(d.reason))
 &&list(v.identityQueue,2000,q=>obj(q)&&row(q.row)&&dict(q.fields,16,str)&&str(q.vendorStatus)&&nullableText(q.provisionalSection)&&typeof q.hidden==='boolean')
 &&list(v.originalRecordRows,2000,row)&&list(v.duplicateKeys,8000,hash)&&Array.isArray(r.potentialDuplicates)&&v.duplicateKeys.length===r.potentialDuplicates.length
 &&obj(c)&&row(c.startRow)&&row(c.endRow)&&(c.endRow as number)-(c.startRow as number)<2000&&list(c.columns,32,x=>num(x,1,16384))&&num(c.rowsCaptured,1,2000)&&num(c.cellsCaptured,1,40000)&&num(c.hiddenRowsIncluded,0,2000)&&num(c.accountedBytes,0,16777216)&&bools(c,['entireWorksheetRowsCaptured','roleColumnsComplete','recordCoverageComplete'])
 &&['headerRows','summaryRows','excludedRows','entityOnlyRows','unassignedRows'].every(k=>list(r[k],2000,row))&&dict(r.fieldColumns,16,x=>num(x,1,16384))
 &&list(r.sections,32,s=>obj(s)&&str(s.id)&&str(s.label)&&nullableText(s.parentId)&&nullableRow(s.headingRow)&&nullableRow(s.firstDataRow)&&list(s.subtotalCandidates,2000,row)&&nullableRow(s.subtotalRow)&&typeof s.boundaryRuleMatched==='boolean'&&str(s.status)&&s.canInsert===false)
 &&list(r.records,2000,x=>obj(x)&&row(x.row)&&nullableText(x.entity)&&nullableText(x.normalizedEntity)&&nullableText(x.sectionId)&&str(x.classification)&&dict(x.identifiers,4,str)&&bools(x,['hidden','markerConflict']))
 &&list(r.potentialDuplicates,8000,x=>obj(x)&&str(x.entity)&&str(x.identifier)&&list(x.rows,2000,row)&&list(x.fields,4,str))
 &&list(r.similarNames,512,x=>obj(x)&&str(x.firstEntity)&&str(x.secondEntity)&&str(x.reason))
 &&list(r.summaryDependencies,2048,x=>obj(x)&&row(x.row)&&num(x.column,1,16384)&&str(x.formulaStatus)&&list(x.localReferences,128,str)&&x.complete===false)
 &&list(r.subtotalCandidates,2048,x=>obj(x)&&row(x.row)&&num(x.column,1,16384)&&str(x.formulaStatus)&&typeof x.configuredBoundary==='boolean')
 &&list(r.diagnostics,4096,x=>obj(x)&&str(x.code)&&str(x.message)&&nullableRow(x.row)&&nullableText(x.sectionId));
}
