# Wiley Widget v1.0 Release Scope

This document defines where the journey to Wiley Widget v1.0 starts.

The start is not a broad bug sweep. The start is a frozen, testable release slice built around the workflows the Town of Wiley actually needs.

## Product Promise For v1.0

Wiley Widget v1.0 ships when the Town of Wiley can do two things with confidence:

1. Understand the current financial situation of each independent enterprise, especially Water, Sewer, Trash, and Apartments.
2. Use JARVIS Chat to ask everyday operational and financial questions in plain language and receive understandable answers that help guide better council decisions.

## Release Pillars

Everything in v1.0 should serve one of these four pillars.

### 1. Navigation And Shell Reliability

The main shell must work every time.

- ribbon or production navigation opens the correct panel
- panel navigation does not fail silently
- JARVIS appears in the right dock when requested
- startup completes without exceptions
- panel restore and runtime navigation do not break the shell

### 2. Panel Quality And Usability

User-facing panels must feel stable and readable.

- panels appear quickly enough to feel responsive
- Syncfusion controls are evenly spaced and readable
- no clipped headers, fields, grids, buttons, or summaries
- layouts hold together at common DPI settings
- interactive controls are wired to real methods and actually work
- tooltips, feedback, and loading states are present where needed

### 3. Database Reliability

The data layer must be trustworthy and documented truthfully.

- database setup and runtime expectations match the docs
- core enterprise data loads reliably
- CRUD operations persist correctly
- the application does not present stale or misleading financial state because of database failures
- recovery and troubleshooting paths are documented clearly enough for maintainers

### 4. Enterprise Financial Understanding + JARVIS

The product must help the Town understand enterprise finances and act on them.

- the app surfaces enterprise-level financial position clearly
- users can move between enterprise-related panels without confusion
- JARVIS can answer everyday questions in everyday language
- JARVIS answers should steer users toward practical financial understanding, not technical jargon
- JARVIS integration must reflect current enterprise context rather than operating as a disconnected novelty

## Proposed In-Scope Panels And Surfaces

These are the primary candidates for v1.0 certification because they map directly to the product promise and the current panel registry.

### Core Navigation

- Enterprise Vital Signs
- JARVIS Chat

### Financial Understanding

- Municipal Accounts
- Budget Management & Analysis
- Revenue Trends
- Department Summary
- Recommended Monthly Charge

### Enterprise Operations

- Customers
- Utility Bills
- Payments

### Supporting Integration

- QuickBooks, only to the extent it is required to support current financial understanding or enterprise workflow continuity

## Proposed Deferred Or Secondary Panels

Unless they are required to prove the v1.0 promise, these should not block release by default.

- Audit and administration surfaces not needed for day-to-day town-clerk decision support
- exploratory analytics or showcase-style panels that do not materially improve enterprise understanding
- low-value polish that does not affect navigation, readability, data trust, or JARVIS usefulness

## Release Questions That Must Be Answered With Proof

### Navigation

- Can every in-scope panel be opened through the production navigation path?
- Does JARVIS always appear in the intended right-dock experience?
- Does startup complete and leave the shell usable?

### Panel Quality

- Are panels readable at 100%, 125%, and 150% DPI?
- Are there any clipped or overlapping Syncfusion controls?
- Do the main actions on each in-scope panel execute the correct method and update the UI correctly?

### Database

- Does the documented database setup match the current code and runtime behavior?
- Can core enterprise data be loaded, edited, and re-read reliably?
- Are failures surfaced clearly rather than leaving users with silent bad state?

### JARVIS

- Can JARVIS be opened reliably from the shell?
- Does it respond in everyday language?
- Does it help answer practical enterprise questions rather than generic chat prompts?
- Does it use current enterprise context well enough to help the town clerk make sense of the financial picture?

## Stop-Ship Conditions For v1.0

v1.0 should not ship if any of these remain true in the release slice.

- a required panel cannot be opened through production navigation
- startup or panel navigation throws or leaves the shell unusable
- in-scope panels contain clipped or unreadable controls in normal usage
- primary user actions are visibly unwired, broken, or inconsistent
- database behavior contradicts the docs or produces unreliable enterprise state
- JARVIS cannot be opened reliably or does not provide understandable enterprise-oriented answers

## Proof Expectations

Use `docs/TESTING_STRATEGY.md` as the governing proof model.

At minimum, v1.0 proof should include:

- startup proof
- panel registry navigation proof for in-scope panels
- hard-fail checks for critical controls on key panels
- targeted tests for shared shell behavior when navigation, theme, layout, or docking changes
- focused database reliability checks for the documented runtime path
- focused JARVIS proof for shell integration and basic enterprise question flow

Do not count these as release evidence:

- `dotnet test WileyWidget.sln` by itself
- filtered runs that execute zero tests
- stale reports or screenshots
- exploratory UI tests used as if they were hard release proof

## First Milestone: RC1 Entry Criteria

The team is ready to begin RC1 only when all of the following are true:

1. The in-scope panel list is accepted.
2. Every in-scope panel is assigned an owner and a status: Certified, Known Limitation, or Deferred.
3. The shell proof lane is trustworthy enough to rerun repeatedly.
4. The current database setup path is documented truthfully.
5. JARVIS integration goals are reduced to a concrete, testable baseline for v1.0.

Use `docs/V1_0_BLOCKER_MATRIX.md` to convert these entry criteria into owned release blockers.

## Practical Starting Sequence

Week 1 should follow this order:

1. Freeze the in-scope panel and workflow list.
2. Run the current proof lane and record what fails.
3. Convert failures into release blockers, not general backlog items.
4. Certify the shell first: startup, navigation, right dock, layout, theme.
5. Certify the highest-value enterprise panels next.
6. Certify JARVIS integration after the shell is trustworthy.
7. Revisit QuickBooks and other optional integrations only if they are necessary for the v1.0 promise.

## Bottom Line

The journey to v1.0 starts with a narrow promise:

Wiley Widget must reliably show the Town of Wiley the financial condition of its enterprises and must make JARVIS useful enough for everyday, plain-language financial guidance.

Everything else is secondary until that slice is proven.
