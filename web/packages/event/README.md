# @axrone/event

A typed, priority-aware, async-friendly event emitter for the Axrone browser
game engine and playable-ad platform.

`@axrone/event` provides the in-process event bus that wires together the
render, physics, scene, asset, input, audio, and tween packages. It is
designed for engine subsystems that must coordinate across frames, survive
pause/resume (offline buffering), observe backpressure, and run inside
strict bundle-size budgets.

The package is `sideEffects: false`, ships ESM and CJS, depends only on
`@axrone/memory`, and is published as part of the `Axrone/web` Lerna
monorepo.

## Installation

```bash
# Inside the Axrone/web monorepo
yarn add @axrone/event
```

```ts
import {
    EventEmitter,
    EventGroup,
    EventScheduler,
    EventUtils,
    createEmitter,
    createTypedEmitter,
    createHooks,
    isEventEmitter,
    filterEvents,
    excludeEvents,
    namespaceEvents,
    mergeEmitters,
    createEventProxy,
} from '@axrone/event';
```

## Quick Start

`EventEmitter` is the workhorse. The minimum useful workflow is
`on` / `emit` / `off`, with `once` for one-shot handlers.

```ts
import { EventEmitter } from '@axrone/event';

interface GameEvents {
    playerHit: { id: number; damage: number };
    scoreChanged: { value: number };
    shutdown: void;
}

const bus = new EventEmitter<GameEvents>();

// Subscribe
const off = bus.on('playerHit', ({ id, damage }) => {
    console.log(`player ${id} took ${damage}`);
});

// Subscribe once
bus.once('shutdown', () => console.log('goodbye'));

// Publish (async — returns Promise<boolean>)
await bus.emit('playerHit', { id: 7, damage: 25 });

// Sync emit (returns boolean, never throws)
bus.emitSync('scoreChanged', { value: 100 });

// Unsubscribe
off();

// Remove every listener on an event
bus.removeAllListeners('playerHit');
```

The `UnsubscribeFn` returned by `on` / `once` is idempotent — calling it
twice is safe.

## EventOptions

Pass an options object to the constructor (or to `createEmitter` /
`createTypedEmitter`) to tune runtime behavior. All keys are optional and
have safe defaults baked in as `DEFAULT_OPTIONS`.

```ts
const bus = new EventEmitter<GameEvents>({
    metrics: true,
    weakReferences: false,
    bufferSize: 2000,
    bufferOverflow: 'drop-oldest',
    gcIntervalMs: 30_000,
    maxListeners: 25,
    concurrencyLimit: 16,
    captureRejections: false,
});
```

| Key                  | Type                                              | Default         | Purpose |
|----------------------|---------------------------------------------------|-----------------|---------|
| `metrics`            | `boolean`                                         | `false`         | If `true`, the emitter records per-event and per-execution timing metrics accessible via `getMetrics()`. When `false`, all metric recording is skipped — no `performance.now()` calls, no Map lookups, no accumulator updates. Strongly recommended to leave off for hot paths. |
| `weakReferences`     | `boolean`                                         | `false`         | If `true`, listener callbacks are held via `WeakRef`. The caller **must** retain a strong reference to the callback — otherwise it is silently collected at the next GC and the subscription stops firing. Intended for long-lived named callbacks, not inline lambdas. |
| `bufferSize`         | `number`                                          | `1000`          | Maximum number of events buffered per event name while paused. |
| `bufferOverflow`     | `'throw' \| 'drop-oldest' \| 'drop-newest'`       | `'drop-oldest'` | Policy when `bufferSize` is exceeded for a paused event. `'throw'` raises an `EventQueueFullError`; `'drop-oldest'` evicts the oldest buffered event; `'drop-newest'` silently drops the new event. |
| `gcIntervalMs`       | `number`                                          | `60000`         | Garbage-collection interval in milliseconds for sweeping dead weak refs, stale metrics, and empty buckets. Set to `0` to disable. The package runs a single shared GC timer; multiple emitters do not multiply the cost. |
| `maxListeners`       | `number`                                          | `10`            | Maximum number of listeners per event before a one-time warning is emitted. Set to `Infinity` to disable. Resettable per-emitter via `resetMaxListeners()`. |
| `concurrencyLimit`   | `number`                                          | `Infinity`      | Maximum number of async listener invocations running concurrently. Set to `Infinity` for unbounded concurrency. |
| `captureRejections`  | `boolean`                                         | `false`         | If `true`, async handler rejections are captured and dispatched as an `error` event instead of being reported as unhandled rejections. |

Constants `DEFAULT_OPTIONS` and `PRIORITY_VALUES` are exported from the
package for consumers that need to read or extend the default policy.

### Priorities

`emit(event, data, { priority })` accepts `'high' | 'normal' | 'low'`.
Listeners are stored in priority buckets and dispatched in
`high → normal → low` order. The same priority bucket is FIFO.

```ts
bus.on('tick', handler, { priority: 'high' });
bus.emit('tick', deltaMs, { priority: 'low' });
```

`emitSync` honors priorities identically and never awaits a handler.

## EventGroup

`EventGroup` is a lifecycle wrapper that tracks every subscription it
creates and tears them all down with a single `dispose()` call. Use it for
anything with a clear begin/end — a scene, a UI panel, a network session,
a level.

```ts
import { EventGroup } from '@axrone/event';

class GameScreen {
    readonly events = new EventGroup<GameEvents>();
    private readonly offMove: () => boolean;

    constructor(bus: EventEmitter<GameEvents>) {
        // Wrap an existing emitter — subscriptions still go through it
        this.events = new EventGroup<GameEvents>(bus);

        this.events.on('playerHit', this.onPlayerHit);
        this.events.once('shutdown', this.onShutdown);

        // Manual subscriptions are also tracked
        this.offMove = this.events.on('scoreChanged', this.onScore);
    }

    destroy(): void {
        this.events.dispose(); // removes every tracked listener
        this.offMove();        // safe to call again — no-op
    }

    // ...handlers
}
```

`EventGroup` is itself a forwarder: it exposes the full
`IEventEmitter` surface so you can `emit` through it. If constructed
without a base emitter, it owns a private `EventEmitter` and disposes it
on `dispose()`. If you pass a base emitter, the group does **not** own
it and leaves its lifetime to the caller.

`EventGroup` returns a `symbol` subscription id from `batchSubscribe` /
`on` / `once` internally, so listeners can be removed by id via
`offById()` even after the original unsubscribe closure is lost.

## EventUtils

`EventUtils` is a stateless toolkit for shaping a single callback. Every
helper takes an `EventCallback<T>` and returns one — they are designed
to be stacked around any `on()` subscription.

```ts
import { EventEmitter, EventUtils } from '@axrone/event';

const bus = new EventEmitter<{ input: string; move: { x: number; y: number }; click: number }>();

// Debounce — fires only after `wait` ms of silence
bus.on('input', EventUtils.debounce((text) => saveDraft(text), 250));

// Throttle — fires at most once per `limit` ms
bus.on('move', EventUtils.throttle(({ x, y }) => updateCursor(x, y), 16));

// RateLimit — at most `maxCalls` invocations per `timeWindow` ms
bus.on('click', EventUtils.rateLimit((n) => registerHit(n), 5, 1000));

// Once — wraps a callback so it only runs the first time
bus.on('startup', EventUtils.once(() => loadConfig()));

// Compose — run several callbacks in order on the same event
bus.on('shutdown',
    EventUtils.compose(flushQueues, persistState, releaseHandles));

// Filter — only invoke when the predicate is true
bus.on('input', EventUtils.filter(
    (text) => text.length > 0,
    (text) => search(text)
));

// Map — transform the payload before the inner callback
bus.on('move', EventUtils.map(
    ({ x, y }) => ({ x: x * 2, y: y * 2 }),
    (scaled) => movePlayer(scaled)
));

// catchErrors — surface async / sync failures to an error handler
bus.on('input', EventUtils.catchErrors(
    (text) => parseQuery(text),
    (err, text) => log.warn('parse failed', err, text)
));
```

| Helper        | Returns | Notes |
|---------------|---------|-------|
| `debounce(fn, waitMs)`        | trailing-edge | The last data wins. Synchronous throws inside the timer are swallowed. Promises returned by `fn` are dehandled (`.catch` ignored) so the timer never produces an unhandled rejection. |
| `throttle(fn, limitMs)`       | leading-edge | Suppressed calls are dropped, not queued. Setting `limit` to `0` makes the wrapper pass-through. |
| `rateLimit(fn, max, windowMs)`| sliding window | Uses an internal ring of timestamps; prunes on every call. |
| `once(fn)`                    | first-call only | Subsequent calls are no-ops. The first returned value is replayed (sync or promise). |
| `compose(...fns)`             | runs sequentially, awaits each | Empty input returns a no-op. Single-input returns the input unchanged. |
| `filter(pred, fn)`            | drops data when `pred` is false | |
| `map(transform, fn)`          | applies `transform` then calls `fn` | |
| `catchErrors(fn, onError)`    | captures both sync `throw` and async rejection | The original `data` is forwarded to `onError`. |

## Combinators (`extras.ts`)

Combinators are functions that wrap one or more emitters and return a
new `IEventEmitter` with a derived event map.

### `namespaceEvents(prefix, source?)`

Returns an emitter whose public event names are all prefixed with
`"${prefix}:"`. If `source` is omitted, the namespace owns a fresh
`EventEmitter`; if provided, the namespace forwards through it and does
**not** dispose it on `dispose()`.

```ts
import { namespaceEvents, createEmitter } from '@axrone/event';

const bus = namespaceEvents<'player', PlayerEvents>('player');

bus.on('player:jump', ({ height }) => {});
bus.emit('player:jump', { height: 3 });
bus.dispose();
```

### `filterEvents(source, allowed, options?)`

Returns a forwarder that only emits the listed events from `source`.
Other events are blocked at the boundary.

```ts
import { filterEvents } from '@axrone/event';

const onlyGameplay = filterEvents(bus, ['playerHit', 'scoreChanged'] as const, {
    passthroughErrors: true, // also forwards 'error'
});

onlyGameplay.on('playerHit', handler); // OK
onlyGameplay.on('shutdown', handler);  // never fires — not in the allow list
```

### `excludeEvents(source, excluded)`

The opposite of `filterEvents`: forwards every event from `source`
except those in `excluded`. Lazily subscribes to events as listeners
attach.

```ts
import { excludeEvents } from '@axrone/event';

const noInternal = excludeEvents(bus, ['debug', 'profile'] as const);
```

### `mergeEmitters(...emitters)`

Returns a new emitter that fans in events from any number of source
emitters. Source emitters that expose the internal event-tap symbol are
forwarded via tap (low overhead); others are bridged lazily on the first
listener.

```ts
import { mergeEmitters } from '@axrone/event';

const ui = createTypedEmitter<UiEvents>();
const net = createTypedEmitter<NetEvents>();

const both = mergeEmitters(ui, net);
both.on('ui:click',  handler);
both.on('net:open',  handler);

both.dispose(); // tears down all forwarders
```

### `createEventProxy(source, target, mapping, transformers?, options?)`

Wires a translation layer between two emitters. Each `sourceEvent` is
mapped to a `targetEvent`; an optional `transformers` map can reshape
the payload. `options.preservePriority` carries the source priority
through to the target; `options.bidirectional` adds the reverse mapping
with cycle detection.

```ts
import { createEventProxy } from '@axrone/event';

const off = createEventProxy(
    inputBus,
    gameBus,
    { pointerDown: 'click', pointerUp: 'release' },
    { pointerDown: (e) => ({ ...e, source: 'touch' }) },
    { preservePriority: true, bidirectional: false }
);

// ...later
off(); // disconnects every forwarding listener
```

### `isEventEmitter(value)`

Duck-type guard. Returns `true` for any value that exposes `on`, `emit`,
and `off` as functions.

```ts
import { isEventEmitter } from '@axrone/event';

function listen(source: unknown) {
    if (!isEventEmitter(source)) return () => false;
    return source.on('change', () => {});
}
```

## createHooks

For component libraries and Svelte/React/Vue adapters, `createHooks`
returns a tightly-scoped, self-contained event surface bound to a
private `EventEmitter`. The returned `useEmitter` lets the consumer
reach the underlying emitter when they need direct access (e.g. for
`EventGroup` wrapping or testing).

```ts
import { createHooks } from '@axrone/event';

function createPlayerController() {
    const hooks = createHooks<PlayerEvents>();

    hooks.on('jump', ({ height }) => player.velocity.y = height);
    hooks.emit('landed', { y: player.y });

    return {
        on: hooks.on,
        off: hooks.off,
        once: hooks.once,
        emit: hooks.emit,
        emitSync: hooks.emitSync,
        emitter: hooks.useEmitter(),
    };
}
```

`createHooks` does not dispose its emitter automatically — pair it with
`EventGroup` (or call `emitter.dispose()`) on teardown.

## Pause / Resume — Offline Buffering

`EventEmitter` can buffer emissions while the consumer is offline (a
tab in the background, a paused scene, a network round-trip) and
re-dispatch them on resume. Pause/resume is emitter-wide and
deterministic: events that arrive while paused are not dropped, they
are queued per event-name in priority order and replayed in arrival
order when `resume()` is called.

```ts
const bus = new EventEmitter<GameEvents>({
    bufferSize: 5000,
    bufferOverflow: 'drop-oldest', // or 'drop-newest' / 'throw'
});

bus.pause();

bus.emit('playerHit', { id: 1, damage: 10 });
bus.emit('playerHit', { id: 2, damage: 20 });
bus.emit('shutdown', undefined);

console.log(bus.getQueuedEvents('playerHit').length); // 2
console.log(bus.isPaused());                          // true

await bus.resume();                                  // drains the buffer
console.log(bus.isPaused());                          // false
```

`EventQueueFullError` is thrown by the emitter only when
`bufferOverflow: 'throw'` is configured and a bucket exceeds
`bufferSize`. The other two policies are silent by design.

Useful buffer APIs:

```ts
bus.pause();                                 // start buffering
bus.resume();                                // stop + drain
bus.isPaused();                              // current state
bus.getQueuedEvents('playerHit');            // peek at a single event
bus.getQueuedEvents();                       // peek at all
bus.getPendingCount('playerHit');            // depth of one bucket
bus.getPendingCount();                       // depth across all events
bus.getBufferSize();                         // total buffered events
bus.clearBuffer('playerHit');                // drop one bucket
bus.clearBuffer();                           // drop everything
await bus.flush('playerHit');                // drain just one event now
await bus.drain({ timeoutMs: 5_000 });       // drain scheduler + buffer
```

`resume()` re-dispatches in priority order (`high → normal → low`) and
preserves arrival order within a priority. The buffer is per-event-name,
so a paused emitter behaves correctly even if subscribers change during
the offline window.

## Performance Tips

`@axrone/event` is built for hot paths. The defaults already bias
toward zero overhead; these are the knobs that matter.

1. **Leave `metrics: false`.** When metrics are off, `emit` skips every
   `performance.now()` call, every `Map` lookup, and every accumulator
   write. The cost of an enabled metrics bucket is roughly 5–10x a bare
   `emit` on V8. Only enable metrics around profiling sessions and
   `resetMetrics()` when you are done.

   ```ts
   const profiler = new EventEmitter<EngineEvents>({ metrics: true });
   // ...exercise...
   const snapshot = profiler.getMetrics('tick');
   profiler.resetMetrics('tick');
   ```

2. **Use `emitSync` for hot paths.** `emit` is async because it may go
   through the scheduler (for bounded concurrency or retry). If your
   handler is synchronous and you do not need backpressure guarantees,
   `emitSync` returns `boolean` immediately, dispatches listeners in
   registration order, and never allocates a promise. Prefer it for
   per-frame events, input, and any synchronous observation hook.

   ```ts
   bus.emitSync('frame', { dt: 16, t: now() });
   ```

3. **Reuse one emitter per subsystem.** Each `EventEmitter` has a
   single shared GC timer, a single internal scheduler, and a single
   per-event listener bucket. Spinning up new emitters per call site
   multiplies bookkeeping. Let one `EventEmitter` per logical
   subsystem route its events and use `EventGroup` for per-component
   lifetime.

4. **Lean on `Subscription` ids for long-lived handlers.** When a
   listener is created far from the place that will remove it, prefer
   `batchSubscribe` and store the returned `symbol` id, then remove via
   `offById`. This avoids a callback identity check on the hot path.

5. **Avoid `weakReferences` unless you really need them.** Weak
   references save a few bytes per listener but force every callback
   to go through a `WeakRef.deref()` on dispatch. Only enable for
   named, long-lived handlers where you control retention.

6. **Bound concurrency for fan-out events.** If one event fans out to
   dozens of async listeners (network, IO), set `concurrencyLimit` to a
   small number (e.g. 8) to avoid memory spikes.

7. **Right-size `bufferSize`.** The buffer is per event-name, so the
   worst-case memory cost is `bufferSize * eventCount`. Pick a value
   that reflects the offline window you actually support, not the
   theoretical maximum.

8. **Dispose on teardown.** `EventEmitter.dispose()` clears every
   listener, stops the GC timer, cancels the scheduler, and releases
   the buffer. Calling it in your teardown path is the single most
   reliable way to avoid listener leaks across scene reloads.

## Reference

For a deep review of the package's invariants, lifecycle guarantees,
and the audit trail behind the public API, see the `docs/architecture/`
directory in the repository root.

## License

Internal — part of the Axrone project. See repository root for license
terms.
