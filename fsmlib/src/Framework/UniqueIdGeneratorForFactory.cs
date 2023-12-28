using MaltiezFSM.API;

namespace MaltiezFSM.Framework
{
    internal class UniqueIdGeneratorForFactory : IUniqueIdGeneratorForFactory
    {
        private int mNextProductId = 0;
        private readonly short mFactoryId;

        public UniqueIdGeneratorForFactory(short factoryId) => mFactoryId = factoryId;
        public int GenerateInstanceId() => mFactoryId + short.MaxValue * mNextProductId++;
        public short GetFactoryId() => mFactoryId;
    }
}
