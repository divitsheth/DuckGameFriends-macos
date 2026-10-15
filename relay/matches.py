"""Reports on recent matches from the relay's match records (see relay.js).

    python3 matches.py              # the last 3 matches, fetched from the Fly relay
    python3 matches.py -n 1 --file /tmp/matches.jsonl

Each player's ping is shown two ways: to the relay (measured by the relay for every
version), and to the other players as the game itself measures it (1.5.2 and later).
Times are US Eastern.
"""
import argparse, json, subprocess, statistics
from datetime import datetime
from zoneinfo import ZoneInfo

ET = ZoneInfo('America/New_York')
p = argparse.ArgumentParser()
p.add_argument('-n', type=int, default=3)
p.add_argument('--file')
args = p.parse_args()
if args.file:
    text = open(args.file).read()
else:
    text = subprocess.run(['fly', 'ssh', 'console', '-a', 'duckfriends-relay', '-q', '-C', 'cat /data/matches.jsonl'],
                          capture_output=True, text=True, check=True).stdout
records = [json.loads(l) for l in text.splitlines() if l.startswith('{')][-args.n:]
if not records: raise SystemExit('No matches recorded yet.')

def pct(xs, q):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(len(xs) * q))] if xs else None
def ms(x): return '-' if x is None else f'{x:.0f}ms'

for r in records:
    start = datetime.fromtimestamp(r['opened'] / 1000, ET)
    minutes = (r.get('closed', r['opened']) - r['opened']) / 60000
    print(f"\nRoom {r['room']}  {start:%a %b %d %I:%M %p} ET  {minutes:.0f} min  {len(r['players'])} players")
    samples = [s for s in r['samples'] if any(v['in'] for v in s['slots'].values())]
    for pl in r['players']:
        slot = str(pl['slot'])
        mine = [s['slots'][slot] for s in samples if slot in s['slots']]
        relay = [v['rtt'][1] for v in mine if v['rtt']]
        spikes = [v['rtt'][2] for v in mine if v['rtt']]
        # The last sample ends when the player quits, with pings still in flight.
        asked = sum(v['probes'] for v in mine[:-1]); answered = sum(v['answers'] for v in mine[:-1])
        gaps = [v['gap'] for v in mine]
        games = [v['game'] for v in mine if v.get('game') and 'conns' in v['game']]
        who = f"slot {slot}{' (host)' if slot == '0' else ''} {pl.get('name') or ''} {pl.get('os') or ''}".strip()
        print(f'  {who}')
        if relay:
            lost = f', {100 * (asked - answered) / asked:.0f}% of pings unanswered' if asked else ''
            print(f'    to relay: median {ms(statistics.median(relay))}, worst 10s {ms(max(relay))}, single worst {ms(max(spikes))}{lost}')
        else:
            print('    to relay: not measured (older than 1.5.2)')
        print(f'    longest silence: {ms(max(gaps, default=None))}; over 500ms in {sum(g > 500 for g in gaps)} of {len(gaps)} samples')
        if games:
            fps = [g['fps'] for g in games if g.get('fps')]
            stall = [g.get('worstFrame', 0) for g in games]
            print(f"    game: {min(fps, default=0)}-{max(fps, default=0)} fps, longest stall {ms(max(stall, default=None))}")
            peers = {}
            for g in games:
                for c in g['conns']: peers.setdefault(c['slot'], []).append(c)
            for other, cs in sorted(peers.items()):
                pings = [c['ping'] for c in cs]
                lost = cs[-1]['lost'] - cs[0]['lost']; sent = cs[-1]['sent'] - cs[0]['sent']
                print(f'    game ping to slot {other}: median {ms(statistics.median(pings))}, 90th {ms(pct(pings, .9))}, peak {ms(max(c["peak"] for c in cs))}; lost {lost} of {sent} sent')
    bad = [s for s in samples if s['lag'] > 50]
    worst = max((s['lag'] for s in r['samples']), default=0)
    print(f"  relay server: worst delay {worst}ms{f', over 50ms in {len(bad)} samples' if bad else ''}")
    rough = []
    for s in samples:
        notes = [f"slot {k} silent {v['gap']}ms" for k, v in s['slots'].items() if v['gap'] > 500]
        notes += [f"slot {k} ping spiked {v['rtt'][2]}ms" for k, v in s['slots'].items() if v['rtt'] and v['rtt'][2] > 2 * max(50, v['rtt'][1])]
        if notes: rough.append(f"    {s['t'] // 60000}:{s['t'] // 1000 % 60:02d}  " + ', '.join(notes))
    if rough: print('  rough moments (minutes into the room):'); print('\n'.join(rough[:20]))
