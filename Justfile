# ClipVault dev tasks. `just` lists them, `just <recipe>` runs one.
# Recipe names mirror the project-template house style (bootstrap / fmt /
# fmt-check / lint / analyze / audit / pre-commit); the implementations are
# .NET. dotnet/typos/etc. are managed by mise, so call them via mise exec
# (does not rely on the shell having mise activated).

dotnet := "mise exec -- dotnet"
typos := "mise exec -- typos"
app := "src/ClipVault.App/ClipVault.App.csproj"
sln := "ClipVault.slnx"
domain_tests := "tests/ClipVault.Domain.Tests/ClipVault.Domain.Tests.csproj"
app_tests := "tests/ClipVault.Application.Tests/ClipVault.Application.Tests.csproj"
infra_tests := "tests/ClipVault.Infrastructure.Tests/ClipVault.Infrastructure.Tests.csproj"
app_ui_tests := "tests/ClipVault.App.Tests/ClipVault.App.Tests.csproj"
coverage_dir := "artifacts/coverage"
publish_dir := "artifacts/publish/win-x64"

# List recipes.
default:
    @just --list --unsorted

# ---------------------------------------------------------------------------
# Bootstrap
# ---------------------------------------------------------------------------

# One-time onboarding: install the toolchain, git hooks, and local dotnet tools, then restore.
bootstrap:
    mise install
    just hooks
    {{dotnet}} tool restore
    {{dotnet}} restore {{sln}}
    @echo "Ready. Try: just build"

hooks:
    mise exec -- lefthook install

hooks-uninstall:
    mise exec -- lefthook uninstall

# ---------------------------------------------------------------------------
# Build, restore, dependencies
# ---------------------------------------------------------------------------

# Build the app (x64).
build:
    {{dotnet}} build {{app}} -p:Platform=x64

# Restore dependencies.
restore:
    {{dotnet}} restore {{sln}}

# Restore with the committed lockfiles enforced (fails if packages.lock.json drifts). Used by CI/Release.
restore-locked:
    {{dotnet}} restore {{sln}} --locked-mode

# Regenerate packages.lock.json after changing dependencies (Directory.Packages.props / *.csproj).
relock:
    {{dotnet}} restore {{sln}} --force-evaluate
    @echo "Lockfiles refreshed. Review and commit the packages.lock.json changes."

# List dependencies with newer versions available (advisory only).
outdated:
    {{dotnet}} list {{sln}} package --outdated

# ---------------------------------------------------------------------------
# Test
# ---------------------------------------------------------------------------

# Run all tests (Domain / Application / Infrastructure / App).
test:
    {{dotnet}} test {{domain_tests}}
    {{dotnet}} test {{app_tests}}
    {{dotnet}} test {{infra_tests}}
    {{dotnet}} test {{app_ui_tests}}

# Run a single test project: just test-one Infrastructure
test-one project:
    {{dotnet}} test tests/ClipVault.{{project}}.Tests/ClipVault.{{project}}.Tests.csproj

# Run tests filtered by name: just test-filter KeyProtector
test-filter pattern:
    {{dotnet}} test {{infra_tests}} --filter "{{pattern}}"

# Re-run a test project on file changes (default: Application). e.g. just watch-test Domain
watch-test project="Application":
    {{dotnet}} watch --project tests/ClipVault.{{project}}.Tests/ClipVault.{{project}}.Tests.csproj test

# ---------------------------------------------------------------------------
# Run lifecycle
# ---------------------------------------------------------------------------

# Stop the resident app (a tray-resident instance locks the DLL and breaks rebuilds).
stop:
    -taskkill //IM ClipVault.App.exe //F

# Stop -> build -> launch (unpackaged, no install).
run: stop build
    {{dotnet}} run --project {{app}} -p:Platform=x64

# Remove build outputs (all of artifacts/, plus legacy bin/obj/dist just in case).
clean: stop
    -rm -rf artifacts dist
    -rm -rf src/*/bin src/*/obj tests/*/bin tests/*/obj

# Clean, then build.
rebuild: clean build

# ---------------------------------------------------------------------------
# Lint, format, and the strict-code grep gate
# ---------------------------------------------------------------------------

# Fix whitespace and code style to match .editorconfig (analyzers are enforced by `just analyze`).
fmt:
    {{dotnet}} format whitespace {{sln}}
    {{dotnet}} format style {{sln}}

# Verify whitespace and code style have no drift (CI / pre-commit; analyzers are enforced by `just analyze`).
fmt-check:
    {{dotnet}} format whitespace {{sln}} --verify-no-changes
    {{dotnet}} format style {{sln}} --verify-no-changes

# Spell-check source, configs, and docs (config in _typos.toml; honors .gitignore).
typos:
    {{typos}}

# Fix typos in place (review the diff before committing).
typos-fix:
    {{typos}} --write-changes

# Lint GitHub Actions workflows.
actionlint:
    mise exec -- actionlint

# yamllint is a Python tool installed via the system package manager (see mise.toml); no-op with a
# one-line hint when it is not installed.

# Lint YAML.
yamllint:
    @if command -v yamllint >/dev/null 2>&1; then \
        yamllint . ; \
    else \
        echo "yamllint not installed locally — 'pipx install yamllint' or 'brew install yamllint' / 'apt install yamllint' to enable this lint pass"; \
    fi

# markdownlint-cli2 is an npm tool that needs a Node runtime, which a .NET project does not otherwise
# pin; no-op with a hint when absent (CI installs Node + markdownlint-cli2 to enforce it).

# Lint Markdown (config in .markdownlint.yaml).
markdownlint:
    @if command -v markdownlint-cli2 >/dev/null 2>&1; then \
        markdownlint-cli2 "**/*.md" "#artifacts" "#**/PULL_REQUEST_TEMPLATE.md" "#**/ISSUE_TEMPLATE/**" ; \
    else \
        echo "markdownlint-cli2 not installed locally — 'npm install -g markdownlint-cli2' (needs Node) to enable this lint pass"; \
    fi

# slnx-wide x64 hits MSB4126, so build App x64 + tests individually. --no-incremental forces a
# full rebuild so analyzers always re-run (incremental builds cache warnings away).

# Analyze all projects with warnings as errors (the C# analog of the template's clippy gate).
analyze: stop
    {{dotnet}} build {{app}} -p:Platform=x64 --no-incremental -warnaserror
    {{dotnet}} build {{domain_tests}} --no-incremental -warnaserror
    {{dotnet}} build {{app_tests}} --no-incremental -warnaserror
    {{dotnet}} build {{infra_tests}} --no-incremental -warnaserror
    {{dotnet}} build {{app_ui_tests}} -p:Platform=x64 --no-incremental -warnaserror

# The Rust template forbids `unsafe` outright; ClipVault legitimately needs `unsafe` for Win32
# interop (see AllowUnsafeBlocks), so this gate enforces the project's suppression policy instead.

# Reject patterns analyzers miss: TODO/FIXME without an issue link, and `#pragma warning disable`.
strict-code:
    #!/usr/bin/env bash
    set -euo pipefail
    echo "::group::strict-code"
    # No bare TODO/FIXME without an issue reference (#NN).
    if grep -rEn '\b(TODO|FIXME)\b' --include='*.cs' --include='*.csproj' src tests docs 2>/dev/null | grep -vE '\(#[0-9]+\)'; then
      echo "bare TODO/FIXME — add a (#NN) issue link"; exit 1
    fi
    # No `#pragma warning disable`: suppress with an attributed, justified [SuppressMessage] instead.
    if grep -rEn '#pragma warning disable' --include='*.cs' src tests 2>/dev/null; then
      echo "#pragma warning disable detected — use [SuppressMessage(..., Justification = \"...\")] instead"; exit 1
    fi
    echo "::endgroup::"

# All static lint passes (no tests). Mirrors the lefthook pre-commit chain plus the YAML/MD/Actions linters.
lint: fmt-check analyze typos actionlint yamllint markdownlint strict-code

# ---------------------------------------------------------------------------
# Audit, coverage
# ---------------------------------------------------------------------------

# NuGetAudit also warns at restore time (warnings are errors), but this is the explicit gate.
# `dotnet list` exits 0 even when vulnerable, so we parse the output and exit non-zero ourselves.

# Fail if any (transitive) dependency has a known advisory.
audit:
    #!/usr/bin/env bash
    set -euo pipefail
    # Force English output so the gate is locale-independent (the dev machine may be localized).
    out=$(DOTNET_CLI_UI_LANGUAGE=en {{dotnet}} list {{sln}} package --vulnerable --include-transitive 2>&1) || { echo "$out"; echo "dotnet list failed"; exit 1; }
    echo "$out"
    if echo "$out" | grep -qi 'has the following vulnerable packages'; then
      echo "ERROR: known-vulnerable packages detected (see above)."; exit 1
    fi
    echo "OK: no known-vulnerable packages."

# Collect line/branch coverage (Cobertura via coverlet) for every test project into artifacts/coverage.
coverage:
    -rm -rf {{coverage_dir}}
    {{dotnet}} test {{domain_tests}} --collect:"XPlat Code Coverage" --results-directory {{coverage_dir}}
    {{dotnet}} test {{app_tests}} --collect:"XPlat Code Coverage" --results-directory {{coverage_dir}}
    {{dotnet}} test {{infra_tests}} --collect:"XPlat Code Coverage" --results-directory {{coverage_dir}}
    {{dotnet}} test {{app_ui_tests}} --collect:"XPlat Code Coverage" --results-directory {{coverage_dir}}
    @echo "Cobertura reports under {{coverage_dir}}/*/coverage.cobertura.xml"

# Build an HTML coverage report (requires the pinned ReportGenerator local tool; opens artifacts/coverage/html).
coverage-html: coverage
    {{dotnet}} tool restore
    {{dotnet}} reportgenerator "-reports:{{coverage_dir}}/**/coverage.cobertura.xml" "-targetdir:{{coverage_dir}}/html" "-reporttypes:Html;TextSummary"
    @echo "Open {{coverage_dir}}/html/index.html"

# ---------------------------------------------------------------------------
# Publish, sign, SBOM
# ---------------------------------------------------------------------------

# -o pins a deterministic output path (the publish profile's PublishDir otherwise lands under
# src/.../bin and hardcodes the TFM).

# Self-contained, install-free distribution.
publish: stop
    {{dotnet}} publish {{app}} -c Release -p:Platform=x64 --self-contained -o {{publish_dir}}
    @echo "Output: {{publish_dir}}/ClipVault.App.exe"

# Pass a version to stamp it: `just sbom 1.2.3` (defaults to 0.0.0). Spec 1.6 for broad tooling support.

# Generate a CycloneDX SBOM for the shipped closure (test + dev deps excluded) into artifacts/sbom.
sbom version="0.0.0":
    {{dotnet}} tool restore
    {{dotnet}} dotnet-CycloneDX {{sln}} -t -ed -o artifacts/sbom -fn clipvault.cdx.json -F Json -spv 1.6 --set-name ClipVault --set-version {{version}}
    @echo "SBOM: artifacts/sbom/clipvault.cdx.json"

# Production releases are signed by SSL.com eSigner (cloud HSM) in the release workflow; this recipe
# covers local/PFX-based signing and reuses signtool from the pinned SDK BuildTools package.

# Authenticode-sign the published exe IF a PFX is configured via env (no-op otherwise, safe to chain).
sign:
    #!/usr/bin/env bash
    set -euo pipefail
    export MSYS_NO_PATHCONV=1   # keep signtool's /flags from being mangled into paths by Git Bash
    exe="{{publish_dir}}/ClipVault.App.exe"
    if [ -z "${CLIPVAULT_PFX:-}" ]; then
      echo "No signing cert configured (CLIPVAULT_PFX unset); leaving the binary unsigned."
      exit 0
    fi
    signtool=$(find "$HOME/.nuget/packages/microsoft.windows.sdk.buildtools" -iname signtool.exe -path '*/x64/*' 2>/dev/null | sort | tail -1)
    [ -n "$signtool" ] || { echo "signtool.exe not found in restored SDK BuildTools"; exit 1; }
    "$signtool" sign /fd SHA256 /f "$CLIPVAULT_PFX" /p "${CLIPVAULT_PFX_PASSWORD:-}" /tr http://timestamp.digicert.com /td SHA256 "$exe"
    "$signtool" verify /pa /v "$exe"

# Mirrors the CI release pipeline so a release can be reproduced locally before tagging. Pass a
# version: `just publish-release 1.2.3`.

# Local dry-run of a release: publish + SBOM + (optional) sign.
publish-release version="0.0.0": (sbom version)
    just publish
    just sign
    @echo "Local release staged: {{publish_dir}}/ + artifacts/sbom/clipvault.cdx.json"

# ---------------------------------------------------------------------------
# Aggregate gates
# ---------------------------------------------------------------------------

# Mirror of the lefthook pre-commit chain so contributors can preview locally (no tests).
pre-commit: fmt-check analyze typos strict-code

# Coverage is collected separately (`just coverage`); ClipVault does not gate on a coverage floor.

# CI gate end-to-end: locked restore, then all static lints, tests, and the dependency audit.
ci: restore-locked lint test audit

# ---------------------------------------------------------------------------
# Misc
# ---------------------------------------------------------------------------

# Check the toolchain (mise's dotnet version).
doctor:
    {{dotnet}} --version
    @echo "app: {{app}}"
