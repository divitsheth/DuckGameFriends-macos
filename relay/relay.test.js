const test = require('node:test');
const assert = require('node:assert');
const dgram = require('node:dgram');
const crypto = require('node:crypto');
const { createRelay } = require('./relay');

function client(port) {
  const s = dgram.createSocket('udp4');
  const inbox = [];
  const waiters = [];
  s.on('message', (m) => (waiters.length ? waiters.shift()(m) : inbox.push(m)));
  return {
    send: (buf) => new Promise((r) => s.send(buf, port, '127.0.0.1', r)),
    next: () => new Promise((resolve, reject) => {
      if (inbox.length) return resolve(inbox.shift());
      const t = setTimeout(() => reject(new Error('timeout')), 1000);
      waiters.push((m) => { clearTimeout(t); resolve(m); });
    }),
    close: () => s.close(),
  };
}
const join = (role, code, nonce = crypto.randomBytes(8), size) =>
  Buffer.concat([Buffer.from([0x4a, role]), Buffer.from(code), nonce, Buffer.from(size ? [size] : [])]);
const data = (token, to, text) => Buffer.concat([Buffer.from([0x44]), token, Buffer.from([to]), Buffer.from(text)]);

test('host and guests exchange packets by slot', async () => {
  const relay = await createRelay({ port: 0, host: '127.0.0.1', log: () => {} });
  const [h, g1, g2] = [client(relay.port), client(relay.port), client(relay.port)];
  try {
    await h.send(join(0, 'QUACK777'));
    const ha = await h.next();
    assert.deepEqual([ha[0], ha[1]], [0x41, 0]);
    await g1.send(join(1, 'QUACK777'));
    const a1 = await g1.next();
    await g2.send(join(1, 'QUACK777'));
    const a2 = await g2.next();
    assert.deepEqual([a1[1], a2[1]], [1, 2]);

    await g1.send(data(a1.subarray(2, 10), 0, 'hi host'));
    const m = await h.next();
    assert.equal(m[1], 1);
    assert.equal(m.toString('utf8', 2), 'hi host');

    await g2.send(data(a2.subarray(2, 10), 1, 'peer'));
    const p = await g1.next();
    assert.equal(p[1], 2);
    assert.equal(p.toString('utf8', 2), 'peer');
  } finally { [h, g1, g2].forEach((c) => c.close()); relay.close(); }
});

test('retried join keeps its slot; bad and full rooms are reported', async () => {
  const relay = await createRelay({ port: 0, host: '127.0.0.1', log: () => {} });
  const c = client(relay.port);
  try {
    await c.send(join(1, 'NOPENOPE'));
    assert.deepEqual([...(await c.next()).subarray(0, 2)], [0x45, 1]);
    await c.send(join(0, 'ROOMROOM'));
    await c.next();
    await c.send(join(0, 'ROOMROOM'));
    assert.deepEqual([...(await c.next()).subarray(0, 2)], [0x45, 3]);
    const nonce = crypto.randomBytes(8);
    await c.send(join(1, 'ROOMROOM', nonce));
    const first = await c.next();
    await c.send(join(1, 'ROOMROOM', nonce));
    const again = await c.next();
    assert.deepEqual(first, again);
    await c.send(join(1, 'ROOMROOM'));
    await c.next();
    await c.send(join(1, 'ROOMROOM'));
    await c.next();
    await c.send(Buffer.concat([Buffer.from([0x51]), Buffer.from('ROOMROOM')]));
    assert.equal((await c.next())[1], 2);
    await c.send(join(1, 'ROOMROOM'));
    assert.deepEqual([...(await c.next()).subarray(0, 2)], [0x45, 2]);
  } finally { c.close(); relay.close(); }
});

test('a host can open an eight-player room', async () => {
  const relay = await createRelay({ port: 0, host: '127.0.0.1', log: () => {} });
  const c = client(relay.port);
  try {
    await c.send(join(0, 'EIGHTDUX', crypto.randomBytes(8), 8));
    assert.equal((await c.next())[1], 0);
    const slots = [];
    for (let i = 0; i < 7; i++) { await c.send(join(1, 'EIGHTDUX')); slots.push((await c.next())[1]); }
    assert.deepEqual(slots, [1, 2, 3, 4, 5, 6, 7]);
    await c.send(join(1, 'EIGHTDUX'));
    assert.deepEqual([...(await c.next()).subarray(0, 2)], [0x45, 2]);
  } finally { c.close(); relay.close(); }
});
