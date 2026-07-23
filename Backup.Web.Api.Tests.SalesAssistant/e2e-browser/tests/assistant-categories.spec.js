const { test, expect } = require('@playwright/test');
const cases = require('../cases.json');

const assistantUrl = (process.env.ASSISTANT_BASE_URL || 'http://euro13/assistant').replace(/\/$/, '');

test.describe('Assistant magasin — catégories (1er tour)', () => {
  for (const c of cases) {
    test(`${c.mainType} · ${c.id}`, async ({ page }) => {
      await page.addInitScript(() => {
        try {
          sessionStorage.clear();
          localStorage.clear();
        } catch (_) { /* ignore */ }
      });

      await page.goto(assistantUrl, { waitUntil: 'networkidle' });
      const input = page.locator('footer.assistant-input textarea, .assistant-input textarea, textarea').first();
      await expect(input).toBeVisible({ timeout: 60_000 });

      const botRows = page.locator('.message-row.bot');
      const before = await botRows.count();

      await input.click();
      await input.fill(c.user);
      await input.dispatchEvent('input');
      const send = page.locator('button.send-btn, button.btn-primary').filter({ hasText: /Envoyer/i });
      await expect(send).toBeEnabled({ timeout: 10_000 });
      await send.click();

      await page.waitForFunction(
        (prev) => {
          const typing = document.querySelector('.message-row.bot .typing');
          const bots = document.querySelectorAll('.message-row.bot');
          const ta = document.querySelector('footer.assistant-input textarea, .assistant-input textarea');
          return !typing && bots.length > prev && ta && !ta.disabled;
        },
        before,
        { timeout: 180_000 }
      );

      // Dernière ligne bot (hors typing) : texte + éventuel tableau produits.
      const lastBotRow = page.locator('.message-row.bot').filter({ hasNot: page.locator('.typing') }).last();
      const lastBot = ((await lastBotRow.locator('.bubble-text').innerText().catch(() => '')) || '').trim();
      const productHay = ((await lastBotRow.locator('.product-table, table, .products').innerText().catch(() => '')) || '');
      const hay = `${lastBot}\n${productHay}`.toLowerCase();
      const productButtons = await lastBotRow.locator('button.add_shopping_cart, button[title*="panier" i], mat-icon', { hasText: /add_shopping_cart|shopping_cart/ }).count().catch(() => 0);
      const cartIcons = await lastBotRow.locator('mat-icon').filter({ hasText: 'add_shopping_cart' }).count();
      const hasProductUi = cartIcons > 0 || /€|\beur\b|prix/i.test(productHay + lastBot);

      expect(lastBot.length, `[${c.id}] réponse bot vide`).toBeGreaterThan(10);

      if (c.mustContainAny.length > 0) {
        const hit = c.mustContainAny.some((token) => hay.includes(String(token).toLowerCase()));
        // Fallback : catalogue UI affiché = recherche produits OK (signal métier).
        expect(
          hit || hasProductUi,
          `[${c.id}] aucun signal parmi [${c.mustContainAny.join(', ')}] et pas de produits UI\n--- bot ---\n${lastBot}`
        ).toBeTruthy();
      }

      for (const bad of c.mustNotContain) {
        expect(
          hay.includes(String(bad).toLowerCase()),
          `[${c.id}] fragment interdit « ${bad} »:\n${lastBot}`
        ).toBeFalsy();
      }

      console.log(`OK ${c.id} · bot=${lastBot.slice(0, 140).replace(/\s+/g, ' ')}…`);
    });
  }
});
