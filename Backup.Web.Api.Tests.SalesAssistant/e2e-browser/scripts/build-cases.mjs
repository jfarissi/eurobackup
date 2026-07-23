import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const scenariosDir = path.resolve(__dirname, '../../Scenarios');
const outPath = path.resolve(__dirname, '../cases.json');
const journeysPath = path.resolve(__dirname, '../journeys.json');

const files = fs
  .readdirSync(scenariosDir)
  .filter((f) => f.endsWith('.json') && f !== 'catalog-maintypes.json')
  .sort();

const cases = [];
const journeys = [];

for (const file of files) {
  const raw = fs.readFileSync(path.join(scenariosDir, file), 'utf8');
  const scenario = JSON.parse(raw);
  const turns = scenario.turns || [];
  if (turns.length === 0 || !turns[0]?.user) continue;

  const mapTurn = (turn, i) => {
    const expect = turn.expect || {};
    return {
      index: i,
      user: turn.user,
      cartBefore: turn.cartBefore || [],
      addFirstProductToCart: i > 0 && (turn.cartBefore?.length > 0),
      mustContainAny: unique([
        ...(expect.replyMustContain || []),
        ...(expect.categoryMustMatchAny || []),
        ...(i === 0 ? scenario.expectedCategoryHints || [] : []),
      ]),
      mustNotContain: unique([
        ...(expect.replyMustNotContain || []),
        ...(expect.productMustNotMatch || []),
        ...(scenario.forbiddenProductHints || []),
      ]),
      wallGuideFamily: expect.wallGuideFamily || null,
    };
  };

  // Smoke 1er tour (toutes catégories)
  const t0 = mapTurn(turns[0], 0);
  cases.push({
    id: scenario.id || path.basename(file, '.json'),
    mainType: scenario.mainType || '',
    title: scenario.title || '',
    user: t0.user,
    mustContainAny: t0.mustContainAny,
    mustNotContain: t0.mustNotContain,
  });

  // Parcours multi-tours (scénarios riches)
  if (turns.length > 1) {
    journeys.push({
      id: scenario.id || path.basename(file, '.json'),
      mainType: scenario.mainType || '',
      title: scenario.title || '',
      turns: turns.map(mapTurn),
    });
  }
}

fs.writeFileSync(outPath, JSON.stringify(cases, null, 2), 'utf8');
fs.writeFileSync(journeysPath, JSON.stringify(journeys, null, 2), 'utf8');
console.log(`Wrote ${cases.length} first-turn cases → ${outPath}`);
console.log(`Wrote ${journeys.length} multi-turn journeys → ${journeysPath}`);

function unique(arr) {
  return [...new Set(
    arr
      .map((s) => String(s).trim())
      .filter((s) => s.length >= 2)
  )];
}
