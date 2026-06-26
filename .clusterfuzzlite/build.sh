#!/bin/bash -eu
# ClusterFuzzLite build for the ClipVault.Fuzz C#/.NET libFuzzer harness.
#
# Produces a single fuzz target, $OUT/clipvault_fuzzer (the libfuzzer-dotnet launcher), plus
# $OUT/clipvault-harness/ — the self-contained, SharpFuzz-instrumented .NET harness it drives, so the
# OSS-Fuzz run image needs no .NET runtime of its own.
#
# The launcher is patched (in the Dockerfile) to self-locate the managed harness when it is run
# without --target_path, so it is a valid standalone OSS-Fuzz target: OSS-Fuzz's bad-build check runs
# the target binary in isolation with no flags, and the launcher then resolves the harness via its own
# location, the build-time $OUT (pinned below), or the run-time /out.

cd "$SRC/clipvault"

harness_out="$OUT/clipvault-harness"
rm -rf "$harness_out"

# 1. Publish the harness self-contained for linux-x64 (no runtime dependency at fuzz time).
dotnet publish tests/ClipVault.Fuzz/ClipVault.Fuzz.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:UseArtifactsOutput=false \
  -o "$harness_out"

# 2. Instrument the assemblies we want coverage for. SharpFuzz rewrites the IL in place so libFuzzer
#    receives edge coverage from ClipVault's own code (not the BCL).
for assembly in ClipVault.Application.dll ClipVault.Domain.dll; do
  sharpfuzz "$harness_out/$assembly"
done

# 3. Compile the self-locating launcher as the single fuzz target, linked with the OSS-Fuzz fuzzing
#    engine + sanitizer flags. BUILD_OUT_TARGET pins the build-time harness path for the bad-build
#    check (which copies the target binary away from $OUT before running it).
$CXX $CXXFLAGS -DBUILD_OUT_TARGET="\"$harness_out/ClipVault.Fuzz\"" \
  "$SRC/libfuzzer-dotnet.cc" -o "$OUT/clipvault_fuzzer" $LIB_FUZZING_ENGINE

# 4. Bundle the seed corpus as OSS-Fuzz expects (<target>_seed_corpus.zip beside the target).
if [ -d "$SRC/clipvault/.clusterfuzzlite/seeds" ]; then
  (cd "$SRC/clipvault/.clusterfuzzlite/seeds" && zip -qr "$OUT/clipvault_fuzzer_seed_corpus.zip" .)
fi
