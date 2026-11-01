# Integration seed

Set `SEED_PROFILE=integration` when running MigrationService against an integration-test database. The service migrates first, then inserts the fixtures below in one transaction. An absent, empty, or other profile only runs migrations. A seed error fails the process.

| Market | Product | Brand | Format | Price (EUR) | Observed on |
| --- | --- | --- | --- | ---: | --- |
| Integration Market A | Integration Milk 1L | Integration Brand | 1 l | 1.25 | 2026-01-01 |
| Integration Market A | Integration Bread | Integration Brand | 1 unit | 2.10 | 2026-01-01 |
| Integration Market B | Integration Pasta | Integration Brand | 1 kg | 3.40 | 2026-01-01 |

Rerunning the seed preserves existing fixture rows. Tests should create their own users, shopping lists, and purchases.
