import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const scenariosDir = path.resolve(__dirname, '../../Scenarios');
const outPath = path.resolve(__dirname, '../cases.json');

const files = fs
  .readdirSync(scenariosDir)
  .filter((f) => f.endsWith('.json') && f !== 'catalog-maintypes.json')
  .sort();

const cases = [];
for (const file of files) {
  const raw = fs.readFileSync(path.join(scenariosDir, file), 'utf8');
  const scenario = JSON.parse(raw);
  const turn = scenario.turns?.[0];
  if (!turn?.user) continue;

  const expect = turn.expect || {};
  cases.push({
    id: scenario.id || path.basename(file, '.json'),
    mainType: scenario.mainType || '',
    title: scenario.title || '',
    user: turn.user,
    mustContainAny: unique([
      ...(expect.replyMustContain || []),
      ...(expect.categoryMustMatchAny || []),
      ...(scenario.expectedCategoryHints || []),
    ]),
    mustNotContain: unique([
      ...(expect.replyMustNotContain || []),
      ...(expect.productMustNotMatch || []),
      ...(scenario.forbiddenProductHints || []),
    ]),
  });
}

fs.writeFileSync(outPath, JSON.stringify(cases, null, 2), 'utf8');
console.log(`Wrote ${cases.length} browser cases → ${outPath}`);

function unique(arr) {
  return [...new Set(
    arr
      .map((s) => String(s).trim())
      .filter((s) => s.length >= 2) // évite "L" / "1." trop permissifs
  )];
}
