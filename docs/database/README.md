# ReportingEngine Database Scripts

Use these SQL Server scripts when you want database changes controlled outside the app startup flow.

- `001_create_reporting_engine_schema.sql`: creates the full V1 application schema on an empty database using the required `RepScdhedularProject_*` table names.
- `002_upgrade_existing_reporting_engine.sql`: upgrades an existing database with `ZipBatchSize`, `KeepLocalFiles`, and nullable `FilePath` support.
- `004_add_smtp_and_failure_profiles.sql`: adds DB-backed SMTP profiles, links EMAIL delivery profiles to SMTP profiles, and adds independent job failure email profiles.

The Admin and Worker projects still include startup checks that create or upgrade the same application columns automatically. These scripts are for DBA review, deployment pipelines, or manual production rollout.

Hangfire creates its own internal tables separately under the configured Hangfire schema.
