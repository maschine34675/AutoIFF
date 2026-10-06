using Comfort.Common;
using EFT;
using UnityEngine;

namespace AutoIFF.Codebase
{
    public class LocalPlayerWatcher : MonoBehaviour
    {
        private Player attachedTo;
        public void Reset()
        {
            attachedTo = null;
        }
        public void Track(Player player)
        {
            attachedTo = player;
        }

        private void Update()
        {
            var gameWorld = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            var mainPlayer = gameWorld != null ? gameWorld.MainPlayer : null;
            if (mainPlayer == null)
            {
                attachedTo = null;
                return;
            }
            if (ReferenceEquals(mainPlayer, attachedTo))
                return;
            if (attachedTo != null)
            {
                var stale = attachedTo.GetComponent<IdentifierManager>();
                if (stale != null)
                    Destroy(stale);
            }

            attachedTo = mainPlayer;
            AutoIffActivation.ActivateFor(mainPlayer, "local player changed — reactivating after respawn");
        }
    }
}
