/**
 * Represents the structure of data payloads received from the Unity pipeline.
 */
export interface UnityUpdatePayload {
    status?: string;
    detail?: string;
    [key: string]: string | number | boolean | undefined;
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
