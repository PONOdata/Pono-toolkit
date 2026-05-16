# Merged into PONOdata/Pono-toolkit as `setup/`

**Original repo:** PONOdata/windows-pono
**Merged:** 2026-05-16 (Cluster consolidation)
**SEND-IT plan reference:** Phase 1B per `COMPACT-PREP-2026-05-15-SEND-IT-CONSOLIDATION.md`
**Source repo to be archived after merge.**

Import paths inside this subdir are NOT yet migrated. The asian-dad QA
gate ignores this subdir via `--ignore=setup` in `pytest.ini` until
per-file path migration lands. To bring this subdir into the gate:

  1. Update imports inside `setup/` to reference the new package path.
  2. `pytest setup/ -v` locally; iterate until green.
  3. Remove the `--ignore=setup` line from `pytest.ini`.
  4. Commit + push.
