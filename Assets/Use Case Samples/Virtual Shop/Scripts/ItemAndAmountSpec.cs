using System;

namespace Unity.Services.Samples.VirtualShop
{
    public struct ItemAndAmountSpec
    {
        public string id;
        public int amount;

        public ItemAndAmountSpec(string id, int amount)
        {
            this.id = id;
            this.amount = amount;
        }

        public override string ToString()
        {
            return $"{id}:{amount}";
        }
    }
}
