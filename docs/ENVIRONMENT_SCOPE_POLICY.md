# Environment Scope Policy

This document describes how environment variables should be documented and used in Wiley Widget.

## Default Policy

- Machine scope is the canonical scope for shared workstation or installed-app runtime configuration.
- User scope is acceptable for local developer setup and migration compatibility.
- Process scope is temporary and should be used only for ad hoc local sessions or scripted handoff.

## Documentation Rules

- Do not imply that secrets belong in the repository.
- When a doc mentions an environment variable, state whether it is expected at machine, user, or process scope.
- If a script supports fallback scopes, document the canonical scope first and the fallback second.

## Release Rule

Release-facing docs should describe the canonical scope, not a one-off convenience setup.
