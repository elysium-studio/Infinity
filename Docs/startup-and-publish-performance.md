# Startup and publish performance

Infinity already publishes with Native AOT, self-contained deployment, invariant globalization, and stripped debugging symbols. The default AOT optimisation remains balanced. Stack traces remain enabled.

## Optional Windows App SDK components

`Build/WindowsAppSdkFeatures.props` excludes the unused AI, ML, MachineLearning, and Widgets package assets from both WinUI projects. The explicit references override the transitive references from the Windows App SDK metapackage and Elysium. The packages remain in the dependency graph, but their compile, runtime, native, and build assets are not consumed.

WinUI, Foundation, InteractiveExperiences, DirectWrite, Win2D, and the runtime resource needed by the existing self-contained deployment remain included. No DLLs are deleted from published output by a post-publish script. Excluding the component build assets also keeps their activation entries out of the generated self-contained manifest.

The versions in this file match Windows App SDK 2.2.0. Review them when upgrading the SDK, and recheck both native dependencies and application usage before adding or excluding a component.

For a comparison with the complete SDK payload, restore and publish with `-p:InfinityIncludeOptionalWindowsAppSdkComponents=true`. Omit that property to use the smaller payload. Always compare fresh output directories: publishing over an old directory can leave obsolete DLLs behind.

The existing 2.0.3-preview output contained approximately 47.03 MiB of these files on x64 and 47.57 MiB on ARM64. These are measurements of excluded files in the old output, not measurements of a newly published executable or compressed installer. Native import checks found no remaining x64 binaries with direct or delay-load imports of the excluded files. Dynamic activation still requires a published-app smoke test.

## AOT comparisons

The release script accepts `-AotOptimization Balanced`, `-AotOptimization Size`, or `-AotOptimization Speed`. Balanced remains the default. The script reports both executable size and total publish-directory size. The release script also signs, packages, and distributes releases; do not use it just to benchmark an experimental setting.

For an isolated comparison, use the normal native Release build prerequisite, then publish the managed project to separate empty directories with the same SDK, architecture, and signing/debugging options. For example:

```powershell
dotnet publish Infinity.Shell.WinUI/Infinity.Shell.WinUI.csproj -c Release -r win-x64 -p:Platform=x64 -p:SelfContained=true -p:PublishAot=true -p:OptimizationPreference=Size -p:DebugType=None -p:DebugSymbols=false -p:StripSymbols=true -o artifacts/aot-size
```

Repeat with `OptimizationPreference=Speed` and omit that property for balanced output. Do not compare the result to a Debug/JIT build. Measure several launches of each version under comparable conditions and separately test first overlay opening, rapid scrolling, and live capture. A smaller executable does not guarantee faster startup, and speed-oriented optimisation can increase executable size.

The old `TrimmerRootDescriptor.xml` was removed because it contained an MSBuild `PropertyGroup`, not a linker descriptor. It was not setting `TrimmerDefaultAction`. Native AOT's normal trimming remains active; no broad assembly-preservation rule or warning suppression was added.

## Validation before release

Restore and project evaluation must succeed for x64 and ARM64. Check that the excluded packages expose only `_._` placeholders and that the SDK component list still includes WinUI, Foundation, InteractiveExperiences, and DirectWrite. Then publish fresh output and test startup, settings, the tour, tray integration, capture, paging, and updater integration. Keep the smaller-payload change separate from any measured choice of AOT optimisation preference.

References: [Native AOT optimisation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing), [NuGet asset controls](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets), [self-contained Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps).
