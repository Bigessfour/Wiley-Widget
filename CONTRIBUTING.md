# Contributing (Solo-Friendly)

Pragmatic notes for future-you (or a curious passerby). Git aliases moved here from README to declutter.

## Git Aliases (Optional)
Add to global config:
```pwsh
git config --global alias.st status
git config --global alias.co checkout
git config --global alias.ci commit
git config --global alias.br branch
git config --global alias.lg "log --oneline --decorate --graph --all"
```
Usage:
```pwsh
git st
git lg
```

## Pre-Push Hook (Optional Gate)
Lightweight guard so you don’t push broken builds.

Setup once:
```pwsh
git config core.hooksPath scripts
```

Hook runs build + tests; non-zero exit blocks push.

## Branching
- main: stable, buildable
- feature/* for risk
- hotfix/* for urgent patch

## Commit Style
Conventional-ish prefixes optional (feat:, fix:, chore:, test:, docs:). Keep commits small & cohesive.

## Release
Use Release workflow to bump version & produce artifact. Tags: vX.Y.Z

## Coverage
CI enforces 70% min line coverage (adjust via COVERAGE_MIN).

## TODO Candidates
- Static analyzers (enable when codebase grows)
- UI smoke automation (FlaUI)
- Dynamic DataGrid column support snippet (see README)

Stay ruthless about scope; this is a scaffold, not a framework.
