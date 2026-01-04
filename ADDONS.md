# SynQPanel Add-ons (Advanced)

## Overview

SynQPanel includes a small set of built-in add-ons that extend the core application with additional runtime and system information.

These add-ons are implemented using SynQPanel’s internal plugin system.

For most users, no interaction with add-ons or plugins is required.

---

## Built-in Add-ons

Current bundled add-ons include:

- **Bluetooth Add-on**
  - Displays Bluetooth adapter status and device visibility

- **Display Session**
  - Provides information about the active display session

- **Panel Runtime**
  - Shows runtime statistics of the SynQPanel process itself

- **System Pulse**
  - A lightweight system overview (uptime, basic load indicators)

These add-ons are maintained as part of the SynQPanel project and are enabled by default.

---

## Advanced: Add-on Architecture

Internally, SynQPanel supports loading external plugins.

This functionality is intended for:
- Developers
- Power users
- Experimental extensions

⚠️ **Important**
- The plugin/Add-on API is not considered stable
- No compatibility guarantees are provided between versions
- External plugins may stop working after updates

---

## Creating a plugin/Add-on (Advanced)

A SynQPanel plugin/Add-on is a .NET class library that implements the internal plugin/Add-on interface and is loaded at runtime.

At a high level:
- plugin/Add-on are discovered from the plugins directory
- Each plugin/Add-on can expose one or more data containers
- Containers provide text or numeric values to the UI

This mechanism is similar to how the built-in add-ons are implemented.

---

## Support Policy

- Built-in add-ons are supported as part of SynQPanel
- External plugins/Add-ons are **not officially supported**
- Issues caused by third-party plugins may require disabling them

---

## Future Direction

plugin/Add-on support may be expanded or formalized in future releases.

For now, SynQPanel focuses on delivering a stable, clean core experience with a curated set of built-in add-ons.
