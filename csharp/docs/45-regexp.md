# Step 7.6: RegExp

## Goals
- Provide `RegExp` constructor and prototype methods `exec`, `test`, `toString`
- Support flags: `g`, `i`, `m`
- Track `lastIndex` for global regexes

## Design
- Backed by `System.Text.RegularExpressions.Regex` with ECMAScript option
- Flags map to .NET options: `i` → IgnoreCase, `m` → Multiline
- `lastIndex` honored for `g` flag; reset to 0 on no match
- `exec` returns an array-like object with captures, `index`, `input`

## Key Code
- `JSContext.InitializeRegExpConstructor()`
  - Stores Regex in `JSObject.HostData`
  - Sets `source`, `flags`, `global`, `ignoreCase`, `multiline`, `lastIndex`
- Prototype: `exec`, `test`, `toString`

## Tests
- `BuiltinsRegExpTests.RegExpExec_MatchesAndReturnsArray`
- `BuiltinsRegExpTests.RegExpTest_GlobalUpdatesLastIndex`

## Notes
- Does not yet implement sticky, unicode, dotAll, or named groups
- `String.prototype.match`/`matchAll`/`replace` are not implemented yet
