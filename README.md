# Into the Void: Unity 6 & AI Agentic Development

Welcome to the **Into the Void** ecosystem. This repository serves as the critical infrastructure for our agentic development workflows, bridging external logic pipelines (like Jules) with the core Unity 6 environment.

## Repository Overview

This project is a hybrid environment containing:
*   **Unity 6 Project**: Located in `GemHunterMatch3/`, showcasing advanced UGS (Unity Gaming Services) implementations.
*   **TypeScript Workspace**: Located in `packages/` and `plugins/`, providing the VS Code extension and tooling for agentic development.
*   **Unity Services Samples**: Root-level `Assets/` and `ProjectSettings/` containing various UGS use case samples.

## Technical Manifest

### Local Environment Setup

To work with the TypeScript workspace and VS Code extension, ensure you have the following installed:

*   **Node.js**: Version 18.x or 20.x (LTS) is required.
*   **TypeScript**: Version 5.x or later.
*   **Unity Editor**: 6000.0.x (Unity 6) or later.
*   **VS Code**: Latest version with the "Extension Development Host" capabilities.

### Compilation Instructions

1.  Navigate to the extension directory:
    ```bash
    cd plugins/vscode-extension
    ```
2.  Install dependencies:
    ```bash
    npm install
    ```
3.  Compile the extension:
    ```bash
    npm run compile
    ```
4.  To package as a `.vsix`:
    ```bash
    npx vsce package
    ```

### Unity & C# Pipeline Integration

The VS Code extension interfaces with the Unity 6 environment via internal C# pipelines.
*   **Entry Point**: `GemHunterMatch3/Assets/GemHunterUGS/Scripts/Core/GameInitializer.cs` manages the initialization of metagame systems.
*   **Cloud Logic**: Server-authoritative logic is located in `GemHunterMatch3/GemHunterUGSCloud/`.

## Binaries & Artifact Management

Compiled artifacts (specifically `.vsix` packages) are distributed via GitHub Releases. Artifact integrity is paramount.

### Retrieval & Verification

1.  Download the latest `.vsix` and `checksums.txt` from the [Releases](https://github.com/evanwilson-arch/com.unity.services.samples.use-cases/releases) page.
2.  Verify the SHA-256 integrity of the artifact before local installation:
    ```bash
    # On Linux/macOS
    sha256sum -c checksums.txt

    # On Windows (PowerShell)
    Get-FileHash ./into-the-void-extension.vsix -Algorithm SHA256
    ```
3.  Install the verified extension in VS Code:
    ```bash
    code --install-extension into-the-void-extension.vsix
    ```

## CI/CD and Automation

We use a sophisticated automation matrix to ensure repository health and reliable deployments:
*   **Release Management**: Handled exclusively by `release-please`. Do not edit `CHANGELOG.md` files manually; they are managed via standardized commit history.
*   **Repository Health**: Continuous linting via `markdownlint` and link verification via `lychee`.
*   **Dynamic Injection**: Unity Services environment variables (`EnvironmentId`, `EnvironmentName`) are dynamically injected into `Settings.json` during the build process to avoid committing sensitive or uninitialized configuration files.

## Contributing

We enforce a strict branching strategy and commit message protocol. Please refer to [CONTRIBUTING.md](CONTRIBUTING.md) for full details on `feature/`, `hotfix/`, and `agent/` branches and Conventional Commits.

## Security

We take security seriously. All vulnerability disclosures should be sent directly to [security@milehigh-world.com](mailto:security@milehigh-world.com). See [SECURITY.md](SECURITY.md) for our full security policy.
