# Prompt: remediate frontend dependency vulnerabilities

Update the frontend dependencies and any affected configuration, application code, and tests to resolve the current npm audit findings. Preserve the existing functionality and the user's uncommitted `package-lock.json` changes; inspect them before making changes. Do not use `npm audit fix --force` blindly or downgrade packages solely to satisfy npm's suggested fix.

## Audit baseline

On 2026-11-13, `npm audit --json --offline=false` from `frontend` reported 27 affected dependency entries: 3 critical and 24 moderate. These are dependency entries, not necessarily 27 distinct vulnerabilities. Re-run an online audit at the start because advisories and package versions change. Ensure npm is actually online: an offline audit previously misleadingly returned zero findings.

- `tinypool@1.1.1` enters through `vitest@3.2.7`. The two critical prototype-pollution gadgets are [GHSA-5gmw-xhrv-c9v3](https://github.com/advisories/GHSA-5gmw-xhrv-c9v3) and [GHSA-85c8-ppgw-ccpr](https://github.com/advisories/GHSA-85c8-ppgw-ccpr); the latter is patched in Tinypool 2.1.2. Vitest 4 removes Tinypool entirely.
- `@storybook/addon-vitest@9.1.5` is also directly affected by critical [GHSA-f2ph-cmg5-wxc8](https://github.com/advisories/GHSA-f2ph-cmg5-wxc8) and high [GHSA-px85-f4q5-jh77](https://github.com/advisories/GHSA-px85-f4q5-jh77). Both reported affected ranges include versions below 9.1.21. Storybook 9.1.20 supports Vitest 4 but is not sufficient for these advisories.
- Moderate findings include Vitest/mocker, Storybook, glob, minimatch, TypeDoc, and typescript-eslint dependency paths. The audit suggests several major changes and even some downgrades; verify actual compatible fixed versions rather than assuming those suggestions are optimal.

## Implementation

1. Find the smallest mutually compatible, maintained dependency set that removes the critical findings and addresses the moderate findings where practical. Consider Vitest 4.1.12 or newer in the 4.x line, since the audit lists `@vitest/mocker` versions below 4.1.12 as affected, and a patched Storybook 9 release at least 9.1.21. Check current advisories, peer dependencies, Node requirements, and all transitive paths: these version floors alone do not guarantee a clean audit. Upgrade Storybook major versions only if needed and document why.
2. Align `vitest`, `@vitest/coverage-v8`, Storybook packages, and browser test dependencies as required by their peer dependencies. Vitest 4 changes browser providers: inspect `vite.config.ts`, replace the string `provider: 'playwright'` with the supported Playwright provider and install `@vitest/browser-playwright` as appropriate. Consult the [Vitest 4 migration guide](https://v4.vitest.dev/guide/migration.html) and the matching Storybook documentation; update `.storybook` configuration if necessary. Preserve Vite's `/api` proxy.
3. Inspect `vitest.unit.config.ts` and all four files in `tests/`. Update test configuration, mocks, assertions, or application code only where required for compatible behavior; retain meaningful coverage of authentication, HTTP handling, item changes, and questionnaires. Do not weaken or delete failing tests merely to pass the upgrade.
4. Update `package.json` and the lockfile with npm, preserving unrelated existing changes. Check the Node version used locally and in CI against package engine requirements; update documented requirements or CI configuration if necessary.
5. Address remaining audit paths with compatible updates where practical. Avoid unverified overrides of major versions. If any finding cannot be fixed safely, record the exact dependency path, advisory, impact, and reason in the final report.

## Verification and acceptance

From `frontend`, run an online `npm audit --offline=false`, `npm ls tinypool vitest` (interpret the absence of Tinypool correctly), `npm run lint`, `npm run build`, `npm test`, and `npm run build-storybook`. Also execute the Storybook browser tests using the repository's Vite/Vitest Storybook project, installing the matching Playwright browser if needed. Confirm all existing tests execute and pass; distinguish the unit test configuration from the Storybook browser project. Verify a clean `npm ci` works with the resulting lockfile, then rerun the appropriate checks if dependency resolution changes.

Report commands and results, the final audit severity counts and any remaining advisories, version changes, test or code changes, and any checks blocked by the environment. Preserve user changes and avoid committing unless asked.
