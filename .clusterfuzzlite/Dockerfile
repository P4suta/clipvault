# Custom ClusterFuzzLite builder for the ClipVault.Fuzz C#/.NET harness.
#
# C# is not a first-class ClusterFuzzLite/OSS-Fuzz language (there is no base-builder-dotnet image),
# so we layer the pieces a .NET libFuzzer target needs onto the generic base-builder:
#   * the .NET SDK (pinned to the repo's toolchain version) to publish a self-contained harness,
#   * the SharpFuzz CLI to instrument the target IL for coverage feedback, and
#   * the libfuzzer-dotnet launcher source, compiled by build.sh against $LIB_FUZZING_ENGINE.
# Everything fetched here is version- or commit-pinned for reproducibility.
FROM gcr.io/oss-fuzz-base/base-builder:v1

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_ROOT=/opt/dotnet \
    PATH=/opt/dotnet:/root/.dotnet/tools:${PATH} \
    # base-builder ships no ICU; run .NET in invariant-globalization mode (the fuzzed code is
    # CultureInvariant regex / Uri / JSON parsing, so behavior is unchanged). The harness itself
    # bakes InvariantGlobalization into its runtimeconfig so it also runs ICU-free at fuzz time.
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Pinned versions: the SDK matches mise.toml; SharpFuzz.CommandLine matches the SharpFuzz library in
# Directory.Packages.props; the launcher is pinned to an exact upstream commit.
ARG DOTNET_SDK_VERSION=10.0.300
ARG SHARPFUZZ_VERSION=2.3.0
ARG LIBFUZZER_DOTNET_REF=bd39d4e88d715ab460a929943645be2a186cde52

# .NET SDK (installed to a fixed prefix on PATH).
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && \
    chmod +x /tmp/dotnet-install.sh && \
    /tmp/dotnet-install.sh --version "${DOTNET_SDK_VERSION}" --install-dir /opt/dotnet && \
    rm -f /tmp/dotnet-install.sh && \
    dotnet --info

# SharpFuzz instrumentation CLI.
RUN dotnet tool install --global SharpFuzz.CommandLine --version "${SHARPFUZZ_VERSION}"

# libfuzzer-dotnet launcher source (the libFuzzer <-> .NET shared-memory bridge); compiled in build.sh.
RUN curl -fsSL \
    "https://raw.githubusercontent.com/Metalnem/libfuzzer-dotnet/${LIBFUZZER_DOTNET_REF}/libfuzzer-dotnet.cc" \
    -o "${SRC}/libfuzzer-dotnet.cc"

# Patch the launcher to self-locate the managed harness when run without --target_path. Stock
# libfuzzer-dotnet dies in that case, but OSS-Fuzz's bad-build check runs the target binary in
# isolation with no flags, so a standalone target must resolve its harness itself: next to the binary,
# then the build-time $OUT (-DBUILD_OUT_TARGET, where bad-build leaves the harness), then run-time /out.
RUN sed -i 's#die("You must specify the target path by using the --target_path command line flag.");#{ static char _p[4096]; ssize_t _n = readlink("/proc/self/exe", _p, sizeof(_p) - 64); if (_n > 0) { _p[_n] = 0; char *_s = strrchr(_p, 0x2f); if (_s) { strcpy(_s, "/clipvault-harness/ClipVault.Fuzz"); if (access(_p, 0) == 0) target_path = _p; } } if (!target_path \&\& access(BUILD_OUT_TARGET, 0) == 0) target_path = BUILD_OUT_TARGET; if (!target_path \&\& access("/out/clipvault-harness/ClipVault.Fuzz", 0) == 0) target_path = "/out/clipvault-harness/ClipVault.Fuzz"; if (!target_path) die("Could not locate the .NET fuzz target."); }#' "${SRC}/libfuzzer-dotnet.cc" \
    && grep -q clipvault-harness "${SRC}/libfuzzer-dotnet.cc"

COPY . ${SRC}/clipvault
WORKDIR ${SRC}/clipvault

# Warm the NuGet cache (packages + linux-x64 runtime packs) while the image build still has network,
# so build.sh can publish even if the compile step runs network-isolated.
RUN dotnet restore tests/ClipVault.Fuzz/ClipVault.Fuzz.csproj -r linux-x64

COPY .clusterfuzzlite/build.sh ${SRC}/
