// Offline smoke tests: no Firebase/Netlify credentials or live services needed.
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { Script, createContext } from 'node:vm';
const js = readFileSync(new URL('../public/js/app.js', import.meta.url), 'utf8');
const rules = readFileSync(new URL('../firebase/firestore.rules', import.meta.url), 'utf8');
const html = readFileSync(new URL('../public/index.html', import.meta.url), 'utf8');
const grab = (re) => { const m = js.match(re); assert.ok(m, `Missing source: ${re}`); return m[0]; };
const funcs = [
  grab(/const filsFromKwd = \(value\) => \{[\s\S]*?\n\};/),
  grab(/const crystalPrice = \(valueFils\) => \{[\s\S]*?\n\};/),
  grab(/function parseProductForm\(v\) \{[\s\S]*?\n\}/),
].join('\n');
const ctx = createContext({ Number, String, Error });
new Script(funcs + '\nthis.test = {filsFromKwd,crystalPrice,parseProductForm}').runInContext(ctx);
const { filsFromKwd, crystalPrice, parseProductForm } = ctx.test;
assert.equal(filsFromKwd('1'), 1000);
assert.equal(filsFromKwd('2.500'), 2500);
assert.equal(crystalPrice(filsFromKwd('2.500')), 2500);
assert.equal(crystalPrice(filsFromKwd('0.125')), 125);
const base = { imageUrl: '', title: 'Walking Headphones', description: 'Wearable reward for active players', rewardType: 'gift', valueKwd: '2.500', quantityOffered: 100 };
const gift = parseProductForm(base);
assert.equal(gift.valueFils, 2500);
assert.equal(gift.quantityOffered, 100);
assert.equal(gift.rewardType, 'gift');
assert.equal(parseProductForm({ ...base, rewardType: 'voucher' }).rewardType, 'voucher');
assert.throws(() => parseProductForm({ ...base, rewardType: 'cashback' }));
assert.throws(() => parseProductForm({ ...base, valueKwd: '1.12345' }));
assert.throws(() => parseProductForm({ ...base, quantityOffered: -1 }));
assert.ok(!html.includes('id="page-export"'));
assert.ok(!html.includes('id="page-sp-requests"'));
assert.ok(!html.includes('data-page="export"'));
assert.ok(html.includes('id="analytics-ranges"'));
assert.ok(html.includes('id="analytics-export-section"'));
assert.ok(html.includes('id="page-sp-verify"'));
assert.ok(js.includes('uploadImage(img.file, "product")'));
assert.ok(js.includes('"/api/generate-description"'));
assert.ok(js.includes('crystalCost: crystalPrice(item.valueFils)'));
assert.ok(!js.includes('CLOUDINARY_CLOUD'));
assert.ok(!js.includes('sponsor_change_requests'));
assert.ok(rules.includes('match /user_rewards/{id}'));
assert.ok(rules.includes('match /sponsor_products/{productId}'));
assert.ok(rules.includes(".affectedKeys().hasOnly([\n          'name','totalSteps','streak'"));
for (const f of ['reward-images', 'generate-description', 'redeem-reward', 'fulfill-reward']) {
  assert.ok(existsSync(new URL(`../netlify/functions/${f}.mjs`, import.meta.url)));
}
console.log('PASS: KD-to-crystal conversion, sponsor form validation, pages, backend endpoints, rules indicators');
