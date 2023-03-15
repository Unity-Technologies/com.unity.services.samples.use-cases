using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Services.Samples.CommandBatching
{
    public class CommandBatchSystem : MonoBehaviour
    {
        public static CommandBatchSystem instance { get; private set; }

        readonly Queue<Command> commandBatch = new Queue<Command>();

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

        public void EnqueueCommand(Command command)
        {
            commandBatch.Enqueue(command);
        }

        public async Task FlushBatch()
        {
            try
            {
                var commandKeys = ConvertCommandBatchToCommandKeys();
                await CallCloudCodeEndpoint(commandKeys);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        string[] ConvertCommandBatchToCommandKeys()
        {
            var batchSize = commandBatch.Count;
            var commandKeys = new string[batchSize];

            for (var i = 0; i < batchSize; i++)
            {
                commandKeys[i] = commandBatch.Dequeue().GetKey();
            }

            return commandKeys;
        }

        async Task CallCloudCodeEndpoint(string[] commandKeys)
        {
            await CloudCodeManager.instance.CallProcessBatchEndpoint(commandKeys);
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
