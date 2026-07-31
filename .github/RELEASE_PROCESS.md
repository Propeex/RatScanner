# Release process

Each user-visible work unit must use a version in `RatScanner/RatScanner.csproj` that has not already been published. Multiple preparatory pull requests for the same unreleased work unit may share that version.

The `Build` workflow performs the following steps:

1. Restores and publishes the Windows x64 self-contained application.
2. Adds the current RatScanner data package and updater.
3. Uploads the unpacked build as a workflow artifact.
4. On a push to `master`, creates a GitHub release named `RatScanner v<version>` with `RatScanner.zip` attached.

Pull requests and manually dispatched workflow runs are build-only validations. They upload an artifact but do not publish a GitHub release.
