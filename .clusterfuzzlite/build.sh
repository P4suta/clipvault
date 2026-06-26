#!/bin/bash -eu
# ClusterFuzzLite build for the ClipVault.Fuzz C#/.NET libFuzzer harness.
#
# Produces a single fuzz target, $OUT/clipvault_fuzzer, made of three pieces:
#   clipvault_fuzzer           - a tiny wrapper. OSS-Fuzz treats any executable whose name ends in
#                                `_fuzzer` as a fuzz target, so this is the entry ClusterFuzzLite runs;
#                                it forwards libFuzzer's args to the launcher with the harness baked in.
#   clipvault_fuzzer.launcher  - the compiled libfuzzer-dotnet driver (a real libFuzzer binary). The
#                                `.launcher` extension keeps OSS-Fuzz from also detecting it as a target.
#   clipvault-harness/         - the self-contained, SharpFuzz-instrumented .NET harness, so the
#                                OSS-Fuzz run image needs no .NET runtime of its own.

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

# 3. Compile the libfuzzer-dotnet launcher, linked with the OSS-Fuzz fuzzing engine + sanitizer flags.
$CXX $CXXFLAGS "$SRC/libfuzzer-dotnet.cc" -o "$OUT/clipvault_fuzzer.launcher" $LIB_FUZZING_ENGINE

# 4. Emit the wrapper ClusterFuzzLite runs. libfuzzer-dotnet only accepts the managed target via
#    --target_path (it never reads the environment), so bake it in and forward all libFuzzer args.
cat > "$OUT/clipvault_fuzzer" <<'EOF'
#!/bin/bash
here="$(dirname "$(readlink -f "$0")")"
exec "$here/clipvault_fuzzer.launcher" \
  --target_path="$here/clipvault-harness/ClipVault.Fuzz" \
  "$@"
EOF
chmod +x "$OUT/clipvault_fuzzer"

# 5. Bundle the seed corpus as OSS-Fuzz expects (<target>_seed_corpus.zip beside the target).
if [ -d "$SRC/clipvault/.clusterfuzzlite/seeds" ]; then
  (cd "$SRC/clipvault/.clusterfuzzlite/seeds" && zip -qr "$OUT/clipvault_fuzzer_seed_corpus.zip" .)
fi
