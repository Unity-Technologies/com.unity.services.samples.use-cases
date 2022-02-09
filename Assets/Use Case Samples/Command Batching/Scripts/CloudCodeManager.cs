using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace CommandBatching
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }

            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            public async Task CallProcessBatchEndpoint(string[] commands)
            {
                if (commands is null || commands.Length <= 0)
                {
                    return;
                }

                try
                {
                    Debug.Log("Processing command batch via Cloud Code...");

                    // Cloud Code API will convert ProcessBatchRequest into a JSON structure like
                    // { batch: { "commands": ["COMMANDBATCH_DEFEAT_RED_ENEMY", "COMMANDBATCH_OPEN_CHEST", etc] }}
                    var result = await CloudCode.CallEndpointAsync<ProcessBatchResult>
                        ("CommandBatch_ProcessBatch", new ProcessBatchRequest(commands));

                    if (result.batchProcessingResult == "failure")
                    {
                        Debug.Log("Cloud Code could not process batch: " + result.batchProcessingErrorMessage);
                    }
                    else
                    {
                        Debug.Log("Cloud Code successfully processed batch.");
                    }
                }
                catch (Exception e)
                {
                    Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    Debug.LogException(e);
                }
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }

            struct Batch
            {
                public string[] commands;

                public Batch(string[] commands)
                {
                    this.commands = commands;
                }
            }

            struct ProcessBatchRequest
            {
                public Batch batch;

                public ProcessBatchRequest(string[] commands)
                {
                    batch = new Batch(commands);
                }
            }

            struct ProcessBatchResult
            {
                public string batchProcessingResult;
                public string batchProcessingErrorMessage;
            }
        }
    }
}
