# SubtitleToolkit Releasing & CI/CD Guide

This guide details the release process, version management, and automated GitHub Actions CI/CD workflows for **SubtitleToolkit**.

---

## 1. Semantic Version Bumping (`./bump.sh`)

Automate Semantic Versioning updates and tag creation using the bundled [`bump.sh`](../bump.sh) script:

```bash
# Bump patch: 1.0.0 -> 1.0.1 (default)
./bump.sh patch

# Bump minor: 1.0.0 -> 1.1.0 (creates dual tags: v1.1.0 and v1.1)
./bump.sh minor

# Bump major: 1.0.0 -> 2.0.0 (creates dual tags: v2.0.0 and v2.0)
./bump.sh major

# Explicit version:
./bump.sh 1.2.3
```

### Dual-Tagging Convention
- Whenever the patch component is `0` (such as `minor` or `major` releases like `1.1.0`), the script automatically generates **two annotated tags**:
  - Full tag: `v1.1.0`
  - Short tag: `v1.1`
- For patch releases where patch is non-zero (e.g. `1.0.1`), only the full tag (`v1.0.1`) is created.

### Pushing Releases
All tags are created as **annotated tags** (`git tag -a -m`), which enables pushing both the version commit and release tags in a single command:

```bash
git push --follow-tags
```

---

## 2. GitHub Actions Workflows

Three dedicated workflows manage continuous integration and multi-registry publishing:

### CI Pipeline ([`.github/workflows/ci.yml`](../.github/workflows/ci.yml))
- **Triggers**: On every `push` and `pull_request` to `master`/`main`.
- **Jobs**:
  - Restores and compiles the solution in `Release` configuration.
  - Runs all 69 unit, property, and golden corpus tests across `net8.0`.
  - Executes `dotnet pack` to enforce `EnablePackageValidation` rules across both `netstandard2.0` and `net8.0`.

### NuGet.org Release ([`.github/workflows/release-nuget.yml`](../.github/workflows/release-nuget.yml))
- **Triggers**: Automatically on tag pushes matching `v*` (e.g. `v1.0.0`), or via manual `workflow_dispatch`.
- **Authentication**: Uses **NuGet OIDC Trusted Publishing** (keyless token exchange) via `NuGet/login@v1` with the configured NuGet package owner (`karanjangid`).
- **Publishing**: Pushes `.nupkg` and `.snupkg` symbol packages to `https://api.nuget.org/v3/index.json`.

### GitHub Packages Release ([`.github/workflows/release-github-packages.yml`](../.github/workflows/release-github-packages.yml))
- **Triggers**: Automatically on tag pushes matching `v*`, or via manual `workflow_dispatch`.
- **Authentication**: Uses the built-in `${{ secrets.GITHUB_TOKEN }}` with `packages: write` permissions.
- **Publishing**: Pushes `.nupkg` to GitHub Packages (`https://nuget.pkg.github.com/kajangid/index.json`).

---

## 3. Public API Freeze & Roslyn Analyzers

When adding or altering public APIs:
1. Compile with Roslyn analyzers enabled.
2. Run `dotnet format analyzers --diagnostics RS0016` to record newly added public symbols into `src/SubtitleToolkit/PublicAPI.Unshipped.txt`.
3. When cutting a major or minor release, consolidate unshipped entries into `src/SubtitleToolkit/PublicAPI.Shipped.txt`.
