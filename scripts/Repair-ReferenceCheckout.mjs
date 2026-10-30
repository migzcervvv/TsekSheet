// Explicit, scoped checkout remediation. Verification never normalizes evidence.
import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const lfInputs = [
  'tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/corpus-v1.json',
  'tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/formatting-v1.json',
  'tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs',
  'scripts/Verify-ExcelReferenceCorpus.ps1',
  'scripts/Verify-ExcelFormattingReferences.ps1',
  'src/TsekSheet.Workbooks/StructuralRowCandidate.cs',
  'src/TsekSheet.Workbooks/StructuralPreservationOracle.cs',
  'tests/TsekSheet.Workbooks.Tests/ReferenceCorpusTests.cs',
  'tests/TsekSheet.Workbooks.Tests/ExcelFormattingReferenceTests.cs',
];
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase();

export function approvedCheckout(blob, working) {
  assert(!blob.includes(Buffer.from('\r')), 'Expected an LF Git blob; do not rewrite unknown history.');
  const windowsCheckout = Buffer.from(blob.toString('utf8').replaceAll('\n', '\r\n'));
  assert(working.equals(blob) || working.equals(windowsCheckout),
    'User modifications or unexpected bytes detected; nothing may be remediated.');
  return blob;
}

function regularPath(root, relative) {
  const target = path.resolve(root, relative);
  assert(target.startsWith(root + path.sep), 'Path escapes repository.');
  for (let current = target; ; current = path.dirname(current)) {
    const stat = fs.lstatSync(current);
    assert(!stat.isSymbolicLink(), 'Redirected paths are forbidden.');
    assert(current !== target || stat.isFile(), 'Expected regular source file.');
    if (current === path.parse(current).root) break;
  }
  return target;
}

export function repair(root, apply = false) {
  root = fs.realpathSync(root);
  const git = args => execFileSync('git', args, { cwd: root, windowsHide: true });
  // Preflight the entire allowlist before writing any file. Staged edits also block.
  const plans = lfInputs.map(relative => {
    const file = regularPath(root, relative);
    const head = git(['show', `HEAD:${relative}`]);
    assert(head.equals(git(['show', `:${relative}`])), `Staged modifications: ${relative}`);
    const before = fs.readFileSync(file);
    const after = approvedCheckout(head, before);
    return { relative, file, before, after };
  });
  const byPath = new Map(plans.map(p => [p.relative, p.after]));
  const corpus = 'tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/';
  const excel = JSON.parse(fs.readFileSync(regularPath(root, corpus + 'VerifiedExcel/corpus.index.json')));
  const formatting = JSON.parse(fs.readFileSync(regularPath(root, corpus + 'VerifiedFormatting/formatting.index.json')));
  for (const [indexFile, evidenceFile] of [
    ['VerifiedExcel/corpus.index.json', 'VerifiedExcel/excel-evidence.json'],
    ['VerifiedFormatting/formatting.index.json', 'VerifiedFormatting/excel-formatting-evidence.json'],
  ]) {
    const evidence = JSON.parse(fs.readFileSync(regularPath(root, corpus + evidenceFile)));
    assert.equal(sha256(fs.readFileSync(regularPath(root, corpus + indexFile))), evidence.indexSha256,
      'Retained index bytes differ from native evidence; do not normalize them.');
  }
  for (const [relative, expected] of [
    [corpus + 'corpus-v1.json', excel.specificationSha256],
    [corpus + 'formatting-v1.json', formatting.specificationSha256],
    ['tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs', excel.fixtureGeneratorSha256],
    ['scripts/Verify-ExcelReferenceCorpus.ps1', excel.excelProcedureSha256],
    ['scripts/Verify-ExcelFormattingReferences.ps1', formatting.scriptSha256],
    ['src/TsekSheet.Workbooks/StructuralPreservationOracle.cs', excel.oracleSha256],
  ]) assert.equal(sha256(byPath.get(relative)), expected, `Retained provenance mismatch: ${relative}`);

  const report = plans.map(p => ({ path: p.relative, beforeSha256: sha256(p.before),
    afterSha256: sha256(p.after), changed: !p.before.equals(p.after) }));
  if (apply) {
    const artifacts = path.join(root, 'artifacts');
    if (!fs.existsSync(artifacts)) fs.mkdirSync(artifacts);
    assert(fs.lstatSync(artifacts).isDirectory() && !fs.lstatSync(artifacts).isSymbolicLink());
    const directory = path.join(artifacts, 'reference-checkout-' + randomUUID());
    // check-ignore returns nonzero if the private backup location is not ignored.
    git(['check-ignore', '--quiet', directory + '/']);
    fs.mkdirSync(directory);
    for (const [i, plan] of plans.entries())
      fs.writeFileSync(path.join(directory, `${i}.before`), plan.before, { flag: 'wx' });
    fs.writeFileSync(path.join(directory, 'fingerprints.json'), JSON.stringify(report, null, 2), { flag: 'wx' });
    for (const plan of plans) if (!plan.before.equals(plan.after)) {
      assert(fs.readFileSync(regularPath(root, plan.relative)).equals(plan.before), 'Source changed after preflight.');
      fs.writeFileSync(plan.file, plan.after);
      assert.equal(sha256(fs.readFileSync(plan.file)), sha256(plan.after));
    }
    return { applied: true, backupDirectory: directory, files: report };
  }
  return { applied: false, files: report };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  assert(process.argv.length <= 3 && (!process.argv[2] || process.argv[2] === '--apply'),
    'Usage: node scripts/Repair-ReferenceCheckout.mjs [--apply]');
  console.log(JSON.stringify(repair(path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..'),
    process.argv[2] === '--apply'), null, 2));
}
