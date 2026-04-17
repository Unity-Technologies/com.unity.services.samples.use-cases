import * as vscode from 'vscode';
import { UnityDataService } from './services/UnityDataService';
import { TelemetryService } from './services/TelemetryService';
import { VisualTelemetryData } from './models/TelemetryData';

/**
 * Activates the Into the Void extension.
 * Orchestrates the integration between Unity services and telemetry observability.
 * @param context The extension context.
 */
export function activate(context: vscode.ExtensionContext): void {
    console.log('[into-the-void] Extension is now active.');

    const dataService = new UnityDataService();
    const telemetryService = new TelemetryService();

    /**
     * Registers the command to synchronize with Unity 6.
     * Uses vscode.window.withProgress for enhanced visual feedback.
     */
    const syncCommand = vscode.commands.registerCommand('into-the-void.syncUnity', async () => {
        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: "Into the Void: Synchronizing with Unity 6",
            cancellable: false
        }, async (progress) => {
            try {
                progress.report({ message: "Fetching project data..." });
                const updates = await dataService.fetchLatestProjectData();

                progress.report({ message: "Processing visual telemetry..." });
                const telemetryEntries: VisualTelemetryData[] = updates.map(update => ({
                    nodeId: update.id,
                    timestamp: update.timestamp,
                    performanceDelta: 0.05, // Example performance delta
                    narrativeDrift: 0.0,    // Example narrative drift
                    metadata: { source: 'sync' }
                }));

                await telemetryService.processBatch(telemetryEntries);

                vscode.window.showInformationMessage(`Sync Complete: Successfully processed ${updates.length} project updates.`);
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                console.error(`[into-the-void] Sync Error: ${message}`);
                vscode.window.showErrorMessage(`Sync Failed: ${message}`);
            }
        });
    });

    context.subscriptions.push(syncCommand);
}

/**
 * Deactivates the extension.
 */
export function deactivate(): void {
    console.log('[into-the-void] Extension is now deactivated.');
}
