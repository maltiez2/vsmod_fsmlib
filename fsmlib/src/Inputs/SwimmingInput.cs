using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using MaltiezFSM.API;

namespace MaltiezFSM.Inputs
{
    public class Swimming : BaseInput, IStatusInput
    {
        bool IStatusInput.CheckStatus()
        {
            return true; // @TODO return mClientApi.World.Player.Entity.Swimming;
        }

        IStatusInput.StatusType IStatusInput.GetStatusType()
        {
            return IStatusInput.StatusType.SWIMMING;
        }
    }
}
