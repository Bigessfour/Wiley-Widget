# Diagnostic Synthesis - 2025-11-12

## EF Optimization Results

- First MunicipalAccount cold compiled+exec query: 842 ms (<1000 ms target ✅)
- Warm repeat execution: 37 ms
- Row count: 72 accounts
- Memory after warm query: 27.45 MB (within early startup GC budget <30 MB)
- Tracking noise: Eliminated (no change-tracking entries; only timing logs emitted)
- Logging scope: Executed SQL only (tracking & sensitive data suppressed)

(Previous content truncated for brevity in this synthesized update.)
