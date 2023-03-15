using System.Collections;
using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.Services.Samples
{
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        // Used to set client authoritative so clients can move the Network Transform.
        // This imposes state to the server and puts trust on your clients.
        // Make sure no security-sensitive features use this transform.
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
