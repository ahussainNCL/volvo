# Public VCTS reference audit

Inspected 2026-10-29 after network access became available. Earlier proxy failures are historical; these pages were fetched successfully over verified HTTPS. These are advertised claims, not tested vehicle behavior or proprietary procedures.

| Public source | Observed advertised claims | Nordlink acceptance implications |
| --- | --- | --- |
| https://volvocartoolapp.com/ | Browser diagnostics, ECU programming, gauges/tables/graphs, recording/CSV, module configuration, fault guide; Open App links to https://cloud.volvocartoolapp.com/ | Online browser access requires an authenticated cloud service and a bridge to Windows hardware; the homepage does not document its internal hardware bridge architecture |
| https://volvocartoolapp.com/diagnostics/ | Whole-vehicle scans, named faults, status/occurrence/aging/timestamps/freeze frames, clearing, confirmation scans, CAN/DoIP, keepalive, functional/physical addressing, reports | Module-supported granularity and individual fields must be measured against licensed definitions and the actual V40; single-code clearing is not assumed universally supported |
| https://volvocartoolapp.com/programming/ | VIN/part-based package selection, secondary bootloader, block sequencing/CRC/timing, progress, post-flash routines, new/used replacement setup, interrupted programming recovery | Licensed firmware and exact module procedures remain prerequisites; advertised resumability is not evidence for our V40 |
| https://volvocartoolapp.com/programming/ | PIN retrieval, central configuration, option adaptation, retrofits, service routines, wiring/repair information, remote engineering support | PIN retrieval does not establish an independently verified reusable CEM decoder algorithm. Support staff are an external service; no integration/account/purchase was made |
| https://volvocartoolapp.com/coverage/ | Classic CAN, SPA/SPA2, CMA; diagnostics, recording, software downloads, configuration, authorised security, service/keys/calibration | Broad platform claims do not identify the actual 2013 V40 CEM/ECM, compatible TOPDON interface, or equipment-specific non-applicability |

The reference site's displayed VIN, module addresses, DIDs, sample DTCs and progress values are presentation examples. They were not copied into Nordlink transport definitions, hardware results or firmware procedures. No app account, subscription, access request, proprietary data download or engineering contact was used. The thirteen-category user baseline remains authoritative; marketing text does not supply missing licensed implementations.

The user selected **online website + Windows helper**, superseding fully local-only presentation for the new cloud component. Existing local gateway controls remain intact. See cloud.md for the separate cloud deployment boundary and current tested scope.
