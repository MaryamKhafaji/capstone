import assert from 'node:assert/strict';
import range from '../netlify/functions/hunt-walking-range.mjs';

for (const [minutes, metres] of [[15, 1000], [30, 2000], [45, 3000], [60, 4000]]) {
  const response = await range(new Request('http://localhost/api/hunt-walking-range', {
    method: 'POST', body: JSON.stringify({ walkingMinutes: minutes }),
  }));
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { walkingMinutes: minutes, maxSearchMeters: metres });
}
for (const minutes of [0, 10, 16, 75, -15, '15', null]) {
  assert.equal((await range(new Request('http://localhost/api/hunt-walking-range', {
    method: 'POST', body: JSON.stringify({ walkingMinutes: minutes }),
  }))).status, 422);
}
assert.equal((await range(new Request('http://localhost/api/hunt-walking-range'))).status, 405);
console.log('PASS: walking time conversion and invalid selection rejection');
