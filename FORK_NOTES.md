# Fork Information

SynQPanel originated from the InfoPanel codebase and was refocused to serve AIDA64 users specifically.

## Upstream Project
- **Name:** InfoPanel
- **Author:** Habib Rehman
- **Repository:** https://github.com/habibrehmansg/infopanel

## Why This Project Diverged

InfoPanel supports multiple hardware monitoring backends (HWInfo, LibreHardwareMonitor) and USB display devices.  
SynQPanel narrows the scope to AIDA64’s shared memory interface, relying on AIDA64 to handle sensor reading and device output, and building a different UI/feature layer on top.

## Major Differences from InfoPanel

- ✅ Added: AIDA64 shared memory support
- ✅ Added: Native AIDA64 sensorpanel handling
- ✅ Added: Flip Clock animations and additional plugins (PanelRuntime, DisplaySession, SystemPulse, BluToothStatus, Weather, TimePulse, etc.)
- ❌ Removed: HWInfo and LibreHardwareMonitor support
- ❌ Removed: USB device support (beadapanel, Turing, etc.)
- 🔁 Ongoing: Independent evolution of the rendering pipeline, plugin model, and UI/UX