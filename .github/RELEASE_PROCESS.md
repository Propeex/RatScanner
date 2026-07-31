# Release process

Every change merged into `master` must include a version update in `RatScanner/RatScanner.csproj`.

The `Build` workflow performs the following steps:

1. Restores and publishes the Windows x64 self-contained application.
2. Adds the current RatScanner data package and updater.
3. Uploads the unpacked build as a workflow artifact.
4. Creates a GitHub release named `RatScanner v<version>` with `RatScanner.zip` attached.

Pull requests run the same build without publishing a release.
