# Step 7.8: Promises

## Goals
- Implement Promise constructor and basic methods: `then`, `catch`, `finally`
- Provide `Promise.resolve` / `Promise.reject`
- Add a microtask queue to `JSContext`

## Design
- Promise state stored on JSObject: `[[PromiseState]]`, `[[PromiseResult]]`
- Reaction queues stored via `HostData` on holder objects
- Microtasks processed via `JSContext.RunMicrotasks()` (tests call this explicitly)

## Key Code
- `JSContext.InitializePromise()`
- `JSContext.EnqueueMicrotask` / `RunMicrotasks`

## Tests
- `BuiltinsPromiseTests.PromiseResolve_Fulfills`
- `BuiltinsPromiseTests.PromiseReject_PropagatesToCatch`

## Notes
- Simplified semantics (no true async; handlers run when `RunMicrotasks` is called)
- `Promise.all`/`race` not implemented
- No unhandled-rejection tracking
