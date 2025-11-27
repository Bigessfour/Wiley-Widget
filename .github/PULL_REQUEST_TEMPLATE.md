## Summary
Describe the slice and the reason for the change in 1–3 sentences.

## Related Issue
<!-- Example: Fixes #12345 -->

## Checklist (pre-merge)
- [ ] Branch name follows naming convention (docs/branching.md)
- [ ] Issue exists with acceptance criteria and test plan
- [ ] Code builds locally (`dotnet build WileyWidget.sln --no-restore`)
- [ ] Unit tests pass locally and added for new code
- [ ] Integration tests added or documented (if applicable)
- [ ] Linters & formatters passed (`dotnet format` + trunk check)
- [ ] Trunk pre-checks passed locally
- [ ] CI checks pass in PR
- [ ] CHANGELOG.md updated (if relevant)

## Testing / Validation Steps
1. How to run tests locally
2. Manual steps to validate UI if relevant (screenshots encouraged)

## Rollback / Recovery
If something goes wrong, detail how to revert or mitigate.

## Notes for Reviewers
- Focus area(s): list files/areas reviewers should focus on.
- Any high-risk changes or notable design decisions.

---

Thanks — small, focused PRs are fastest to review. If this is a large change, please split it into sub-slices and use the branching strategy described in docs/branching.md.