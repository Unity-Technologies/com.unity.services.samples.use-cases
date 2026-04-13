# Contributing to Into the Void

Thank you for your interest in contributing to the **Into the Void** ecosystem. To maintain architectural integrity and ensure seamless automated workflows, we enforce a strict set of contribution protocols. **Non-compliant contributions will be rejected.**

## Mandatory Branching Strategy

All contributions **must** follow our strictly defined branching model. Pull Requests from branches that do not follow this naming convention will not be reviewed. Branches must be prefixed according to their purpose:

*   **`feature/`**: New functional development or significant enhancements.
*   **`hotfix/`**: Critical patches for production-ready code.
*   **`agent/`**: AI-driven development tasks, automated architectural refinements, and infrastructure-as-code updates.

**Example:** `feature/integration-unity-6`, `agent/refactor-ts-services`, `hotfix/memory-leak-fix`.

## Mandatory Commit Message Protocol

We strictly adhere to the [Conventional Commits](https://www.conventionalcommits.org/) specification (v1.0.0). This is critical for our automated versioning and changelog generation via `release-please`.

**Format:** `<type>(<scope>): <description>`

### Required Types:
*   `feat`: A new feature
*   `fix`: A bug fix
*   `docs`: Documentation only changes
*   `style`: Changes that do not affect the meaning of the code (white-space, formatting, linting, etc)
*   `refactor`: A code change that neither fixes a bug nor adds a feature
*   `perf`: A code change that improves performance
*   `test`: Adding missing tests or correcting existing tests
*   `build`: Changes that affect the build system or external dependencies
*   `ci`: Changes to our CI configuration files and scripts
*   `chore`: Other changes that don't modify src or test files

**Note:** Breaking changes must be indicated by a `!` after the type/scope or by `BREAKING CHANGE:` in the footer.

## Development Workflow

1.  **Fork and Clone**: Create a personal fork and clone it locally.
2.  **Branch**: Create a new branch with the mandatory prefix (`feature/`, `hotfix/`, `agent/`).
3.  **Code**: Implement your changes following our TypeScript and Unity architectural guidelines. Enforce strict type safety and modular structures.
4.  **Verify**: Ensure all tests pass, linters are satisfied (`npm run lint`), and your changes do not introduce regressions.
5.  **Commit**: Use Conventional Commit messages. **This is not optional.**
6.  **Push and PR**: Push to your fork and submit a Pull Request to the `main` branch.

## Pull Request Guidelines

*   Provide a clear and descriptive title following Conventional Commit types.
*   Reference any related issues.
*   Include screenshots or videos for UI/Unity changes.
*   **All CI/CD checks (linting, link checking, build) must pass before a review will be conducted.**

By contributing, you agree that your contributions will be licensed under the project's LICENSE.
