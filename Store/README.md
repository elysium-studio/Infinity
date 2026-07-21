# Microsoft Store publishing

The release flow builds Infinity as a Native AOT, self-contained x64 application, packages that output as an MSIX, creates an `.msixupload` bundle with symbols, and submits it with the Microsoft Store Developer CLI.

Configure these values from the app identity page in Partner Center:

- `INFINITY_STORE_PRODUCT_ID`
- `INFINITY_STORE_IDENTITY_NAME`
- `INFINITY_STORE_PUBLISHER`
- `INFINITY_STORE_PUBLISHER_DISPLAY_NAME`

Configure these credentials for unattended submission:

- `INFINITY_STORE_TENANT_ID`
- `INFINITY_STORE_SELLER_ID`
- `INFINITY_STORE_CLIENT_ID`
- `INFINITY_STORE_CLIENT_SECRET`

Credentials can instead be stored by running `msstore reconfigure` once. Do not commit credentials to the repository.

`publish.ps1` publishes to the normal release channels and the Microsoft Store by default. Use `-SkipMicrosoftStore` to omit the Store, `-MicrosoftStoreDraft` to leave the submission uncommitted, or `-MicrosoftStoreFlightId` to submit to a flight.

The Store package can be generated without submitting it:

```powershell
.\scripts\Publish-MicrosoftStore.ps1 -Version 1.22.1-preview -InputDirectory .\Publish\1.22.1-preview\Assets -PackageOnly
```

Store-installed builds use Store servicing and do not initialise Velopack. Direct-download builds continue to use Velopack.
