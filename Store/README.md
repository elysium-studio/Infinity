# Microsoft Store publishing

The release flow builds Infinity as a Native AOT, self-contained x64 application, packages that output as an MSIX, creates an `.msixupload` bundle with symbols, and submits it with the Microsoft Store Developer CLI.

Copy `publish.local.example.json` to `publish.local.json` and fill in the settings for the local machine. `publish.local.json` is ignored by Git.

Settings are resolved in this order:

1. A parameter passed directly to `publish.ps1`.
2. An `INFINITY_*` environment variable.
3. The corresponding value in `publish.local.json`.

The Microsoft Store identity values come from the app identity page in Partner Center:

- `INFINITY_STORE_PRODUCT_ID`
- `INFINITY_STORE_IDENTITY_NAME`
- `INFINITY_STORE_PUBLISHER`
- `INFINITY_STORE_PUBLISHER_DISPLAY_NAME`

Configure these credentials for unattended submission:

- `INFINITY_STORE_TENANT_ID`
- `INFINITY_STORE_SELLER_ID`
- `INFINITY_STORE_CLIENT_ID`
- `INFINITY_STORE_CLIENT_SECRET`

Store credentials can instead be stored by running `msstore reconfigure` once. The SFTP password and Store client secret are plaintext if placed in `publish.local.json`; use user-level environment variables instead if that is undesirable. Do not commit credentials to the repository.

`publish.ps1` publishes to the normal release channels and the Microsoft Store by default. Use `-SkipMicrosoftStore` to omit the Store, `-MicrosoftStoreDraft` to leave the submission uncommitted, or `-MicrosoftStoreFlightId` to submit to a flight.

The complete release is run through the single repository script:

```powershell
.\publish.ps1
```

Use `-MicrosoftStorePackageOnly` to build the Store package without submitting it.

Store-installed builds use Store servicing and do not initialise Velopack. Direct-download builds continue to use Velopack.
