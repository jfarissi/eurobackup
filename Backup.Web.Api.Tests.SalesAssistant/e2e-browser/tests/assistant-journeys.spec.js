const { test, expect } = require('@playwright/test');
const journeys = require('../journeys.json');

const assistantUrl = (process.env.ASSISTANT_BASE_URL || 'http://euro13/assistant').replace(/\/$/, '');

async function sendAndWaitBot(page, userText) {
  const input = page.locator('footer.assistant-input textarea, .assistant-input textarea, textarea').first();
  const botRows = page.locator('.message-row.bot').filter({ hasNot: page.locator('.typing') });
  const before = await botRows.count();

  await input.click();
  await input.fill('');
  await input.fill(userText);
  await input.dispatchEvent('input');
  const send = page.locator('button.send-btn, button.btn-primary').filter({ hasText: /Envoyer/i });
  await expect(send).toBeEnabled({ timeout: 10_000 });
  await send.click();

  await page.waitForFunction(
    (prev) => {
      const typing = document.querySelector('.message-row.bot .typing');
      const bots = document.querySelectorAll('.message-row.bot:not(:has(.typing))');
      const ta = document.querySelector('footer.assistant-input textarea, .assistant-input textarea');
      return !typing && bots.length > prev && ta && !ta.disabled;
    },
    before,
    { timeout: 180_000 }
  );

  const lastBotRow = page.locator('.message-row.bot').filter({ hasNot: page.locator('.typing') }).last();
  const lastBot = ((await lastBotRow.locator('.bubble-text').innerText().catch(() => '')) || '').trim();
  const productHay = ((await lastBotRow.innerText().catch(() => '')) || '');
  const hay = `${lastBot}\n${productHay}`.toLowerCase();
  const cartIcons = await lastBotRow.locator('mat-icon').filter({ hasText: 'add_shopping_cart' }).count();
  const hasProductUi = cartIcons > 0 || /€/.test(productHay);
  return { lastBot, hay, hasProductUi, lastBotRow };
}

async function addFirstVisibleProduct(page, lastBotRow) {
  const addBtn = lastBotRow.locator('button').filter({ has: page.locator('mat-icon', { hasText: 'add_shopping_cart' }) }).first();
  if (await addBtn.count() === 0) {
    // Fallback: n'importe quel add du message
    const any = page.locator('.message-row.bot').last().locator('mat-icon', { hasText: 'add_shopping_cart' }).first();
    if (await any.count() === 0) return false;
    await any.click();
  } else {
    await addBtn.click();
  }
  // Laisse le panier se mettre à jour
  await page.waitForTimeout(800);
  return true;
}

test.describe('Assistant magasin — parcours multi-tours', () => {
  for (const journey of journeys) {
    test(`${journey.mainType} · ${journey.id} (${journey.turns.length} tours)`, async ({ page }) => {
      test.setTimeout(180_000 * journey.turns.length);

      await page.addInitScript(() => {
        try {
          sessionStorage.clear();
          localStorage.clear();
        } catch (_) { /* ignore */ }
      });

      await page.goto(assistantUrl, { waitUntil: 'networkidle' });
      await expect(page.locator('textarea').first()).toBeVisible({ timeout: 60_000 });

      for (const turn of journey.turns) {
        // Avant un tour qui attend un panier : ajouter le 1er produit de la réponse précédente.
        if (turn.index > 0 && turn.addFirstProductToCart) {
          const prevRow = page.locator('.message-row.bot').filter({ hasNot: page.locator('.typing') }).last();
          const added = await addFirstVisibleProduct(page, prevRow);
          console.log(`[${journey.id} t${turn.index}] addToCart=${added}`);
        }

        const { lastBot, hay, hasProductUi } = await sendAndWaitBot(page, turn.user);
        expect(lastBot.length, `[${journey.id} t${turn.index}] bot vide`).toBeGreaterThan(10);

        if (turn.mustContainAny.length > 0) {
          const hit = turn.mustContainAny.some((token) => hay.includes(String(token).toLowerCase()));
          expect(
            hit || hasProductUi,
            `[${journey.id} t${turn.index}] aucun signal parmi [${turn.mustContainAny.join(', ')}]\n--- bot ---\n${lastBot}`
          ).toBeTruthy();
        }

        for (const bad of turn.mustNotContain) {
          expect(
            hay.includes(String(bad).toLowerCase()),
            `[${journey.id} t${turn.index}] interdit « ${bad} »:\n${lastBot}`
          ).toBeFalsy();
        }

        if (turn.wallGuideFamily) {
          const focusPatterns = {
            Structure: /1\.\s*Structure[\s\S]{0,120}← à choisir maintenant/i,
            Binder: /2\.\s*Ciment[\s\S]{0,120}← à choisir maintenant/i,
            Reinforcement: /3\.\s*Treillis[\s\S]{0,120}← à choisir maintenant/i,
            Tools: /4\.\s*Outillage[\s\S]{0,120}← à choisir maintenant/i,
          };
          const re = focusPatterns[turn.wallGuideFamily];
          if (re) {
            expect(
              re.test(hay),
              `[${journey.id} t${turn.index}] focus checklist ≠ ${turn.wallGuideFamily}\n${lastBot}`
            ).toBeTruthy();
          }
        }

        console.log(`OK ${journey.id} t${turn.index} · ${lastBot.slice(0, 100).replace(/\s+/g, ' ')}…`);
      }
    });
  }
});
