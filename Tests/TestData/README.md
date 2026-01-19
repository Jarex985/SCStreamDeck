# Test Data

This directory holds shared test fixtures for the test suite.

## Required assets (provided by user)
- `LIVE-keybindings.json`: Copy your real keybindings export here for integration coverage.
- `Data.p4k`: Place a copy of the Star Citizen Data.p4k here, or set environment variable `SCSTREAMDECK_DATA_P4K_PATH` to an absolute path.

## Sample fixtures (checked in)
- `actionmaps_sample.xml`: Minimal CryXML action map sample for parser tests.
- `localization_sample.xml`: Minimal localization XML sample for parser tests.
- `appsettings.test.json`: Optional test settings file to point integration tests at your data paths.
