# SynQPanel Display Guide

## Overview

SynQPanel is designed to render system information panels within standard Windows display environments.

Panels are displayed as windows and can be positioned on:
- Primary monitors
- Secondary monitors
- Small auxiliary displays recognized by Windows

SynQPanel does not directly manage or communicate with display hardware.

---

## Display Requirements

Any display that:
- Is detected by Windows
- Supports standard desktop rendering

can be used with SynQPanel.

Display resolution, orientation, and scaling are managed by Windows.

---

## Panel Layout Considerations

When designing panels for small or secondary displays:
- Use compact layouts
- Prefer text and simple gauges
- Avoid excessive animation
- Match panel resolution to the display resolution

---

## Data Source

Hardware telemetry is provided via **AIDA64 Shared Memory**.

A compatible AIDA64 installation with Shared Memory enabled is required.

AIDA64 is a registered trademark of FinalWire Ltd.
SynQPanel is an independent project and is **not affiliated with or endorsed by FinalWire Ltd.**

---

## Notes

SynQPanel does not include:
- USB display drivers
- Panel firmware tools
- Hardware-specific performance optimizations

Hardware behavior and performance depend entirely on the display device and Windows configuration.
