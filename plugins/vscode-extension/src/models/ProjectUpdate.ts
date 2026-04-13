/**
 * Represents the structure of data payloads received from the Unity pipeline.
 */
export interface UnityUpdatePayload {
    /** Current synchronization status of the project */
    status?: 'synchronized' | 'out_of_sync' | 'error';
    /** Detailed message or description of the update */
    detail?: string;
    /** Optional error code if the status is 'error' */
    errorCode?: number;
    /** Additional metadata for the update */
    metadata?: Record<string, string | number | boolean>;
}

/**
 * Standardized interface for project updates within the Into the Void ecosystem.
 */
export interface ProjectUpdate {
    /** Unique identifier for the update */
    id: string;
    /** Unix timestamp of when the update was generated */
    timestamp: number;
    /** Strongly-typed payload containing the update details */
    payload: UnityUpdatePayload;
}
