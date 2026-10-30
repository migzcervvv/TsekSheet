import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { approvedCheckout, lfInputs, repair, sha256 } from './Repair-ReferenceCheckout.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const attributes = fs.readFileSync(path.join(root, '.gitattributes'));
const corpus = 'tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/';
const specification = corpus + 'formatting-v1.json';
const index = corpus + 'VerifiedExcel/corpus.index.json';
const binary = corpus + 'VerifiedExcel/before-subtotal.excel.xlsx';
const immutableCapture = corpus + 'FormatResolution/capture.json';

test('Scoped remediation accepts exact LF/CRLF checkout bytes and refuses edits', () => {
  const lf = Buffer.from('first\nsecond\n');
  assert.deepEqual(approvedCheckout(lf, lf), Buffer.from('first\nsecond\n'));
  assert.deepEqual(approvedCheckout(lf, Buffer.from('first\r\nsecond\r\n')), lf);
  for (const invalid of ['first\nchanged\n', 'first\r\nsecond\n', '\uFEFFfirst\nsecond\n'])
    assert.throws(() => approvedCheckout(lf, Buffer.from(invalid)), /modifications/);
  assert.throws(() => approvedCheckout(Buffer.from('first\r\n'), Buffer.from('first\r\n')), /LF Git blob/);
});

for (const autocrlf of ['true', 'false']) test(`Git checkout under core.autocrlf=${autocrlf} preserves literal evidence conventions`, () => {
  const sandbox = fs.mkdtempSync(path.join(os.tmpdir(), 'TsekSheet-portability-'));
  // Retain the isolated Git test repository for diagnosis; never touch the real index.
  const git = args => execFileSync('git', args, { cwd: sandbox, windowsHide: true });
  git(['init', '--quiet']);
  git(['config', 'core.autocrlf', autocrlf]);
  git(['config', 'core.eol', autocrlf === 'true' ? 'crlf' : 'lf']);
  fs.writeFileSync(path.join(sandbox, '.gitattributes'), attributes);
  const literalFiles = [
    [specification, Buffer.from('specification\nline two\n')],
    [index, Buffer.from('{\r\n  "captured": true\r\n}\r\n')],
    [binary, Buffer.from([0x50, 0x4b, 0x0d, 0x0a, 0x00, 0xff])],
    [immutableCapture, Buffer.from('{\r\n  "captured": true\r\n}\r\n')],
    ['unrelated.txt', Buffer.from('ordinary\ntext\n')],
  ];
  for (const [relative, bytes] of literalFiles) {
    fs.mkdirSync(path.dirname(path.join(sandbox, relative)), { recursive: true });
    fs.writeFileSync(path.join(sandbox, relative), bytes);
  }
  git(['add', '--', '.gitattributes', ...literalFiles.map(([relative]) => relative)]);
  for (const [relative] of literalFiles) fs.unlinkSync(path.join(sandbox, relative));
  git(['checkout-index', '--all']);
  assert.deepEqual(fs.readFileSync(path.join(sandbox, specification)), Buffer.from('specification\nline two\n'));
  assert.deepEqual(fs.readFileSync(path.join(sandbox, index)), Buffer.from('{\r\n  "captured": true\r\n}\r\n'));
  assert.deepEqual(fs.readFileSync(path.join(sandbox, binary)), Buffer.from([0x50, 0x4b, 0x0d, 0x0a, 0x00, 0xff]));
  assert.deepEqual(fs.readFileSync(path.join(sandbox, immutableCapture)), Buffer.from('{\r\n  "captured": true\r\n}\r\n'));
  assert.deepEqual(fs.readFileSync(path.join(sandbox, 'unrelated.txt')),
    Buffer.from(autocrlf === 'true' ? 'ordinary\r\ntext\r\n' : 'ordinary\ntext\n'));

  // Also check actual retained bytes, including every binary fixture, in a fresh index.
  const newBundle = fs.readdirSync(path.join(root, corpus, 'FormatResolution')).filter(p => p.endsWith('.json') || p.endsWith('.xlsx')).map(p => corpus + 'FormatResolution/' + p);
  const files = [...new Set([...execFileSync('git', ['ls-files', '-z', '--', corpus], { cwd: root }).toString().split('\0')
    .filter(p => p.endsWith('.json') || p.endsWith('.xlsx')), ...newBundle])];
  for (const relative of [...lfInputs, ...files]) {
    const target = path.join(sandbox, relative);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, fs.readFileSync(path.join(root, relative)));
  }
  git(['add', '--', ...new Set([...lfInputs, ...files])]);
  for (const relative of new Set([...lfInputs, ...files])) fs.unlinkSync(path.join(sandbox, relative));
  git(['checkout-index', '--', ...new Set([...lfInputs, ...files])]);
  for (const relative of new Set([...lfInputs, ...files]))
    assert.equal(sha256(fs.readFileSync(path.join(sandbox, relative))),
      sha256(fs.readFileSync(path.join(root, relative))), relative);
});

test('Retained native evidence binds exact specifications, procedures and every output', () => {
  const read = relative => fs.readFileSync(path.join(root, relative));
  const json = relative => JSON.parse(read(relative));
  const excel = json(corpus + 'VerifiedExcel/corpus.index.json');
  assert.equal(sha256(read(corpus + 'corpus-v1.json')), '5B3C043F3821F6CFED956D280033EF7EA64F6C7E682081280A44935FBFD07747');
  assert.equal(sha256(read(corpus + 'formatting-v1.json')), '2C148FB71178662CFFF4CEAE961B1EABD114626984AFED638C0C6C4AB42A86E7');
  assert.equal(sha256(read('tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs')), excel.fixtureGeneratorSha256);
  const groups = [
    ['VerifiedExcel/', 'corpus.index.json', 'excel-evidence.json', 'scripts/Verify-ExcelReferenceCorpus.ps1', 'excelProcedureSha256', 4],
    ['VerifiedFormatting/', 'formatting.index.json', 'excel-formatting-evidence.json', 'scripts/Verify-ExcelFormattingReferences.ps1', 'scriptSha256', 17],
  ];
  for (const [directory, indexFile, evidenceFile, procedure, hashField, count] of groups) {
    const base = corpus + directory, manifest = json(base + indexFile), evidence = json(base + evidenceFile);
    assert.match(evidence.provenance, /^Microsoft Excel/);
    assert.equal(sha256(read(base + indexFile)), evidence.indexSha256);
    assert.equal(sha256(read(procedure)), manifest[hashField]);
    assert.equal(evidence.scriptSha256, manifest[hashField]);
    for (const item of manifest.cases) {
      assert.equal(sha256(read(base + item.inputFile)), item.inputSha256);
      if (item.candidateFile) assert.equal(sha256(read(base + item.candidateFile)), item.candidateSha256);
    }
    assert.equal(evidence.cases.length, count);
    for (const item of evidence.cases) {
      const original = manifest.cases.find(c => c.spec.id === item.id);
      assert(original);
      assert.equal(item.inputSha256, original.inputSha256);
      assert.equal(sha256(read(base + item.outputFile)), item.outputSha256);
    }
  }
  const base = corpus + 'FormatResolution/', manifest = json(base + 'resolution.index.json'), capture = json(base + 'capture.json');
  assert.equal(sha256(read(base + 'resolution.index.json')), capture.indexSha256);
  assert.equal(sha256(read('scripts/Capture-FormatResolution.ps1')), capture.scriptSha256);
  assert.equal(manifest.procedureSha256, capture.scriptSha256);
  assert.equal(sha256(read('tests/TsekSheet.Workbooks.Tests/FormatResolutionFixture.cs')), manifest.generatorSha256);
  assert.equal(capture.cases.length, 19);
  for (const item of manifest.cases) assert.equal(sha256(read(base + item.inputFile)), item.inputSha256);
  for (const item of capture.cases) if (item.status === 'observed') assert.equal(sha256(read(base + item.outputFile)), item.outputSha256);
});

test('Remediation preflights all inputs and preserves unstaged and staged user changes', () => {
  const sandbox = fs.mkdtempSync(path.join(os.tmpdir(), 'TsekSheet-modified-checkout-'));
  execFileSync('git', ['clone', '--quiet', '--no-hardlinks', '--no-checkout', '--', root, sandbox], { windowsHide: true });
  const git = args => execFileSync('git', args, { cwd: sandbox, windowsHide: true });
  git(['config', 'core.autocrlf', 'true']);
  git(['read-tree', 'HEAD']);
  fs.writeFileSync(path.join(sandbox, '.gitattributes'), attributes);
  git(['add', '--', '.gitattributes']);
  git(['checkout-index', '--all']);
  const pristine = repair(sandbox);
  assert(pristine.files.every(file => !file.changed));
  const modified = path.join(sandbox, lfInputs.at(-1));
  fs.appendFileSync(modified, '// User modification must survive.\n');
  const before = lfInputs.map(relative => fs.readFileSync(path.join(sandbox, relative)));
  assert.throws(() => repair(sandbox, true), /modifications/);
  for (const [i, relative] of lfInputs.entries())
    assert.deepEqual(fs.readFileSync(path.join(sandbox, relative)), before[i]);
  assert(!fs.existsSync(path.join(sandbox, 'artifacts')), 'No partial mutation or backups before complete preflight.');
  git(['add', '--', lfInputs.at(-1)]);
  assert.throws(() => repair(sandbox, true), /Staged modifications/);
  assert.deepEqual(fs.readFileSync(modified), before.at(-1));
});
