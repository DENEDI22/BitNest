# BitNest — Retrospective

> Living document. Updated after each milestone. Append new milestones at the top.

---

## Milestone: v0.0.3 — Auth + Sharepoint + Installer

**Shipped:** 2026-03-26
**Phases:** 5 (6–10) | **Plans:** 14 | **Timeline:** 7 days

### What Was Built

- JWT auth with PBKDF2 hashing, refresh rotation, and bearer-protected endpoints
- Auth-first frontend with session bootstrap (signup/login/logout)
- Admin panel (list/disable/create users) and owner/grant-based file access enforcement
- Secure sharepoint download links with expiry and SHA256 token storage
- Dropbox-style upload slots with atomic capacity enforcement and public upload page
- Linux x86_64 Textual TUI installer — 67-test unit suite, Docker Compose orchestration, sudo guard

### What Worked

- **TDD scaffold-first approach** paid off on every phase — test failures caught integration issues before executor agents touched production code
- **Human checkpoint plans** (06-03, 07-03, 08-02, 09-02, 10-04) were the right call for browser/terminal flows that can't be headlessly verified
- **Wave-based parallel execution** kept phases with multiple plans fast — especially phases 6 and 7
- **Atomic task commits** made debugging regressions trivial — git bisect would have been easy

### What Was Inefficient

- **Phase 10 tracked outside ROADMAP.md** — installer was developed without a formal roadmap entry, causing confusion in `/gsd:progress` (showed 4/4 phases complete when 5 were done)
- **ROADMAP.md progress table fell stale** — phases 7–8 stayed "In Progress / Not started" even after completion; the CLI's roadmap-complete detection relies on checkbox format consistency
- **Some SUMMARY.md one-liner fields left as placeholder** — the `One-liner:` header without content tripped `summary-extract` in MILESTONES.md generation

### Patterns Established

- Use `python -m pytest installers/.../tests/` as the standard automated check before human installer checkpoints
- Add a root/privilege guard at installer entry point, not mid-install
- For TUI work: `@work(thread=True)` required on all Textual async decorators since v0.89+

### Key Lessons

- Add phases to ROADMAP.md at planning time, not after execution — the CLI's phase discovery depends on it
- Keep SUMMARY.md `One-liner:` field populated immediately after plan execution, not as an afterthought
- The `call_from_thread` API moved to `App` in Textual v0.89+ — document breaking-change patterns in CONTEXT.md when known

### Cost Observations

- Model mix: primarily sonnet (executor + verifier), opus (planner)
- Sessions: ~15 conversations across 7 days
- Notable: human checkpoints (3 browser verifications + 1 terminal verification) were zero-cost and caught real integration issues

---

## Cross-Milestone Trends

| Metric | v1.0 (historical) | v0.0.3 |
|--------|-------------------|--------|
| Phases | 5 (est.) | 5 |
| Plans | est. 10 | 14 |
| Timeline | unknown | 7 days |
| Files changed | unknown | 138 |
| Human checkpoints | 0 | 4 |
| Regressions caught | unknown | 2 (TUI runtime, expiry UI) |

---
