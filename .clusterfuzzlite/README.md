# ClusterFuzzLite for ClipVault

Continuous fuzzing of `ClipVault.Application`'s untrusted-input parsing surface — the
secret-detection classifiers and the insight parsers (JSON / URL / content-kind). Clipboard
content is attacker-influenced, so these must never hang (ReDoS) or throw on hostile input.

## Why this is a custom builder

C#/.NET is not a first-class ClusterFuzzLite/OSS-Fuzz language (there is no `base-builder-dotnet`
image). The `Dockerfile` layers what a .NET libFuzzer target needs onto the generic base-builder:
the pinned .NET SDK, the [SharpFuzz](https://github.com/Metalnem/sharpfuzz) instrumentation CLI, and
the [libfuzzer-dotnet](https://github.com/Metalnem/libfuzzer-dotnet) launcher (the libFuzzer ⇄ .NET
shared-memory bridge).

## Files

| File | Role |
| --- | --- |
| `Dockerfile` | Builder image: pinned .NET SDK + SharpFuzz CLI + launcher source. |
| `build.sh` | Publishes the harness self-contained, instruments the IL, compiles the launcher, assembles `$OUT/clipvault_fuzzer`. |
| `project.yaml` | Engine/sanitizer config (`c++` toolchain, libFuzzer, ASan). |
| `seeds/` | Seed corpus (dummy JSON / URL / secret-shaped tokens) to bootstrap coverage. |

The harness project lives at `tests/ClipVault.Fuzz/`. It is intentionally **not** in `ClipVault.slnx`,
so the Windows `--locked-mode` restore and the analyze/coverage gates stay untouched.

## The single fuzz target

`build.sh` emits one target:

- `$OUT/clipvault_fuzzer` — the compiled libfuzzer-dotnet launcher (a real libFuzzer binary, so
  OSS-Fuzz detects and instrument-checks it). Stock libfuzzer-dotnet only takes its managed target via
  `--target_path`, but OSS-Fuzz's bad-build check runs the target with no flags, so the Dockerfile
  patches it to **self-locate** the harness: next to the binary, then the build-time `$OUT`
  (`-DBUILD_OUT_TARGET`, where bad-build leaves the harness), then the run-time `/out`.
- `$OUT/clipvault-harness/` — the self-contained, instrumented .NET harness it drives (no runtime
  needed at fuzz time). Not a libFuzzer binary, so it is never mistaken for a second target.

## Validate locally

Requires Docker (Linux containers):

```bash
# Build the image.
docker build -t clipvault-cflite -f .clusterfuzzlite/Dockerfile .

# Build the fuzzer into a local $OUT and smoke-run it for 30s.
docker run --rm -e SANITIZER=address -e FUZZING_LANGUAGE=c++ \
  -v "$PWD/out:/out" clipvault-cflite \
  bash -c 'compile && /out/clipvault_fuzzer -runs=100000 -max_total_time=30 /out/seeds || true'
```

CI runs this on every PR (`cflite-pr.yml`, code-change mode) and weekly in batch (`cflite-cron.yml`).
