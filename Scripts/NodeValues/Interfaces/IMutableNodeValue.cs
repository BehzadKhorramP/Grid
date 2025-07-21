using System;

namespace MadApper.GridSystem
{
    public interface IMutableNodeValue : INodeValue
    {
        public const string k_RandomTag = "X";
        public enum Type { Replaceable, Alterable, AlterableWhenRandom }


        public static Func<IMutableNodeValue, NodeValue> s_TryMutating;

        public static Func<ValueData, ValueData> s_TryAlteringValueData;

        public Type i_MutationType { get; }     

        public void i_OnReplacedEntirely(NodeValue newNodeValue);
        public void i_OnValueDataAltered(ValueData newValueData);

    }
}
