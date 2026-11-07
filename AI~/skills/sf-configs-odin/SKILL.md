---
name: sf-configs-odin
description: Use when editing SFramework JSON configurations through the Odin Inspector SFConfigsWindow or changing that editor integration.
---

# sf-configs-odin

Read [the package guide](references/package-readme.md) and [installed source](SOURCE.md).

Require installed Odin assemblies and ODIN_INSPECTOR. Keep custom integrations in an Editor assembly referencing SFramework.Configs.Odin.Editor; this package provides no runtime assembly.

Use SFConfigsWindow and the shared SFConfigsEditorUtility cache. Configure discoverable Assets paths; runtime JSON also needs a Resources location. A missing menu/cache is a dependency, define, path, or Type-discriminator issue to inspect first.

Save replaces the source JSON and updates the Unix timestamp Version. Reload discards unsaved changes and can trigger default-file creation. Save As copies edited node data, while New constructs a fresh instance. Both use the selected filename as the root Id and require an unused path under Assets.

Keep writes inside the project's Assets folder and stage the complete JSON before committing it to disk. Validate node IDs against saved files of the same config type.

Verify editing, saving, reload, new IDs and runtime discovery in the consuming project's desktop Unity Editor with Odin installed.
