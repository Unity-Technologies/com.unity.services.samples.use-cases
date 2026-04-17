/**
 * Represents performance and narrative telemetry data for visual systems.
 * Part of the Narrative Observability Hub integration.
 */
export interface VisualTelemetryData {
    /** The delta in visual performance metrics (e.g., frame time variance) */
    performanceDelta: number;
    /** Measured drift from the intended narrative sequence */
    narrativeDrift: number;
    /** Unix timestamp of the telemetry capture */
    timestamp: number;
    /** Identifier for the specific visual node or effect */
    nodeId: string;
    /** Optional contextual metadata */
    metadata?: Record<string, string | number | boolean>;
}
