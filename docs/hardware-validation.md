# Windows / hardware-validation queue

No item below has hardware evidence. Use the same actual 2013 V40 D2 and trusted reference tool; do not replace missing dependencies with non-applicability.

| Gate | Needed evidence / dependency | Current state |
| --- | --- | --- |
| 1 Foundation | Exact TOPDON model, official installer/version, Windows architecture, both registry views, actual FunctionLibrary and signature metadata; reproduce reported missing DLL, repair via official installer; x86/x64 worker checks; DPAPI round-trip under current Windows user | Windows unavailable; missing-library report unverified |
| 2 Identification | Established licensed/documented bus, addressing and session definitions; VIN read matching vehicle; CEM/ECM and all responding module part, HW/SW identities; supported voltage/status procedure comparison | Definitions and vehicle absent |
| 3 Diagnostics | Licensed definition schema/importer; same-vehicle/reference scan comparisons; DTC flags/counters/timestamps/freeze frames and supported clear scope; live scaling/units/gaps; actual recording/replay/CSV comparison | Real transport and licensed packs absent |
| 4 Own CEM decoder | Public research/licence audit, exact CEM applicability, established reusable-PIN algorithm; checkpoint identity/algorithm version; ≥3 distinct independently verified challenges plus fresh connection; actual PIN DPAPI storage/reveal | Algorithm/applicability and real challenges absent |
| 5 Configuration/service | Original backup and demonstrated supported restoration before edits; exact setting/retrofit/routine prerequisites; per-procedure verified readback/result | Procedures and restoration evidence absent |
| 6 Programming/replacement | Licensed firmware catalogue/package format, VIN/module matching; bootloader/block/checksum/post-routine procedures; stable power; replay plus bench/spare interruption evidence; precise recovery and new/used adaptation procedures | Firmware access/procedures/bench evidence absent |
| 7 Repair/security | Licensed VIN-relevant local repair content/schema; individual documented key/mileage/security procedures and authorised providers | Content/procedures absent |
| 8 Release/assistance | Existing provider/documented handoff; previewed redaction tests; signed installer, clean Windows user/machine install/uninstall; final per-operation capability review | Portable Windows binaries cross-published on Linux; Windows execution/installer/provider unverified |

For gate 1 record screenshots/log excerpts with secrets excluded, exact TOPDON model/serial policy, official installer source, Windows build, registry view, registered library, file hash, PE architecture, version and Authenticode result. Do not copy proprietary DLLs into Git. Run the worker tests, DPAPI Windows test and actual USB removal/hang/crash recovery checks. A mocked process exit is useful infrastructure evidence but does not validate a real driver.

For every hardware acceptance capture: test ID; date; operator; environment=hardware; real VIN; module identifiers; tool/definition/firmware hashes and licence references; exact procedure/preconditions; expected vs measured result; interruptions; restoration/recovery evidence; storage/export review. Do not put the PIN itself into this record.

Only evidence caused by actual vehicle equipment can justify `equipmentNonApplicable`. A missing definition, algorithm, provider, subscription or implementation remains `blocked`.

Online helper: run the published Windows helper against the eventual HTTPS Heroku origin; validate official driver inspection/open/close and native crash/USB-removal behavior through the authenticated relay. Verify local PIN reveal remains unavailable remotely, local stores survive helper restarts and no cloud disconnect silently repeats commands. Cloud browser tests on Linux are infrastructure evidence only.
