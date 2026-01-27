# ChattyBuddy

ChattyBuddy is a lightweight WPF peer-to-peer chat utility that uses Tailscale to discover and connect to devices on your mesh network.

## Features
- Lightweight floating chat window
- Dark / Light theme that follows Windows app theme
- Persistent chat history per device
- Nudge feature with custom sound
- Auto-start with intelligent wait for Tailscale on boot
- Built-in update checker (downloads installer from GitHub Releases)

## Requirements
- Windows 10/11
- Tailscale installed and logged in
- .NET runtime or use the self-contained published EXE

## Install (recommended)
1. Download the installer from the Releases page: https://github.com/folliejester/ChattyBuddy/releases
2. Run the installer. It will bundle the published app and can download the Tailscale installer if missing.

## Build (developer)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
Place the publish output into the installer folder before building the Inno Setup script.

## Usage
- Launch ChattyBuddy, select a Tailscale device, then click Connect.
- The app auto-start option registers a run key; on boot ChattyBuddy waits silently for the Tailscale network to be available before showing the UI.

## Configuration & Assets
- Custom nudge sound (Optional, already shipped with one): place `Nudge.wav` in `Assets` next to the installed EXE.
- App icon (Optional again, already shipped with one) asset live in `Assets/` in the project.

## Troubleshooting
- If auto-start fails, verify the Run registry entry at `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`.
- If update downloads are flagged by AV, submit the binary to VirusTotal and request vendor reanalysis; code-signing reduces false positives.

## Contributing
Fork the repo, create a branch, implement changes, and open a PR. See CONTRIBUTING.md in repo (if present).

## License
MIT — see LICENSE in the repository.

## Releases
Releases and installers are available at https://github.com/folliejester/ChattyBuddy/releases
