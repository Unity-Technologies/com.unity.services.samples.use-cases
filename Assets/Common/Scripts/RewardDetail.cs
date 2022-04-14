using System;

namespace UnityGamingServicesUseCases
{
    [Serializable]
    public struct RewardDetail
    {
        public string id;
        public long quantity;
        public string spriteAddress;

        public override string ToString()
        {
            return $"{quantity} {id}";
        }
    }
}
