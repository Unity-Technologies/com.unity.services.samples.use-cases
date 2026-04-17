# Bolt's Journal - Critical Learnings

## 2025-05-14 - [Flood-Fill Optimization in Match-3 Logic]
**Learning:** In a performance-critical simulation loop (like a Match-3 board check), $O(n)$ lookups using `List<T>.Contains()` during flood-fill operations cause exponential performance degradation as match sizes grow. Even `HashSet<T>` has hashing overhead and potential allocations.
**Action:** Use a pre-allocated `bool[,]` bitmask for the grid. This provides $O(1)$ lookups with zero heap allocation during the search. Always use `System.Array.Clear()` to reset the bitmask, as it is more efficient than manual loops.

## 2025-05-14 - [GC Pressure in VR Environments]
**Learning:** Frequent small allocations (like `new List<T>()` or `new Stopwatch()`) in the `Update` loop or during rapid events (like cascading matches) trigger the Garbage Collector, leading to frame-rate hitches that are particularly disruptive in VR.
**Action:** Pre-allocate all collection buffers (`Stack`, `List`) and utility objects (`Stopwatch`) at the class level and reuse them via `.Clear()` or `.Restart()`.
