# SynQPanel

SynQPanel is a desktop panel visualization application for Windows, designed to display hardware telemetry and system information in clean, customizable visual layouts.

It focuses on **panel-based presentation**, allowing users to design compact, information-dense displays for desktops or secondary screens.

---

## What is SynQPanel?

SynQPanel provides a visual layer on top of hardware telemetry data, enabling users to create dashboards composed of text, gauges, bars, tables, and other visual elements.

The application is built with WPF, follows an MVVM architecture, and supports extensibility through a controlled add-on system.

SynQPanel is designed to be **minimal, precise, and visually intentional**, prioritizing clarity over excess.

---

## Key Features

- Panel-based system visualization
- Custom layouts with precise positioning
- Multiple visualization elements (text, gauges, bars, tables)
- Profile-based configurations
- Built-in add-ons for runtime and system context
- External display support (monitor-based)

---

## Data Source

SynQPanel retrieves hardware telemetry via **AIDA64 Shared Memory**.

A compatible AIDA64 installation with Shared Memory enabled is required to access sensor data.

---

## Panels & Visualization

Panels are fully customizable and can be tailored to show only the information you care about.  
Layouts can be adapted for different resolutions, orientations, and display use cases.

---

## Add-ons

SynQPanel includes a small set of built-in add-ons that demonstrate runtime, system, and session information.

The add-on architecture is intentionally minimal, focused on stability and long-term maintainability.

---

## Requirements

- Windows 10 / 11
- AIDA64 with Shared Memory enabled

---

## Status

SynQPanel is currently in **beta**.  
Features, add-ons, and internal APIs may evolve as development continues.

---

## Copyright & Attribution

Original project copyright © 2024 Habib Rehman  
Modifications and ongoing development © 2025 SynQPanel contributors

SynQPanel is derived from an open-source project originally authored by Habib Rehman and released under the GNU General Public License (GPL).  
The codebase has since been significantly modified, extended, and maintained independently by the SynQPanel contributors.

---

## License & Disclaimer

SynQPanel is free software licensed under the **GNU General Public License v3.0 or later**.

You are free to use, modify, and redistribute this software under the terms of the GPL.  
See the LICENSE file for full details.

AIDA64 is a registered trademark of FinalWire Ltd.  
SynQPanel is an independent project and is **not affiliated with or endorsed by FinalWire Ltd.**
