using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using KrokoshaCasualtiesMP;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChangeSkinMP
{
    /// summary
    /// Main ChangeSkin component which handles replacement of (fore)skin.
    public class ChangeBody : MonoBehaviour
    {
        public void Init(NetBody _netBody)
        {
            OwnerID = _netBody.netId;
            NBody = _netBody;
            foreach (
                SpriteRenderer spriteRenderer in NBody.body.gameObject.GetComponentsInChildren<SpriteRenderer>()
            )
            {
                foreach (string name in names)
                {
                    if (spriteRenderer.sprite != null && spriteRenderer.sprite.name == name)
                        spriteRenderers.Add(spriteRenderer);
                }
            }
        }

        // Single-player path: no NetBody exists without a multiplayer lobby.
        // Build the same renderer list straight from the player rig GameObject.
        public void Init(GameObject bodyRoot)
        {
            OwnerID = 0;
            NBody = null;
            foreach (
                SpriteRenderer spriteRenderer in bodyRoot.GetComponentsInChildren<SpriteRenderer>()
            )
            {
                foreach (string name in names)
                {
                    if (spriteRenderer.sprite != null && spriteRenderer.sprite.name == name)
                        spriteRenderers.Add(spriteRenderer);
                }
            }
        }

        /// <summary>
        /// Bind a NetBody to a ChangeBody that the SP fallback created without
        /// one. Reuses the already-captured spriteRenderers list (does NOT
        /// re-scan, which would duplicate entries). Called by
        /// NetworkRegistry.RegisterConnected when transitioning SP -> MP on the
        /// same player rig.
        /// </summary>
        internal void BindNetBody(NetBody netBody)
        {
            NBody = netBody;
            OwnerID = netBody.netId;
        }

        public void ApplySkin(SkinObject _skin)
        {
            bool wasWorking = Working;
            if (wasWorking)
                RepEnd();
            Skin = _skin;
            foreach (SpriteReplacer replacer in spriteReplacers)
            {
                replacer.Restore();
            }
            spriteReplacers.Clear();
            foreach (KeyValuePair<string, Sprite> keyValuePair in Skin.BodySprites)
            {
                foreach (SpriteRenderer spriteRenderer in spriteRenderers)
                {
                    if (spriteRenderer.sprite.name == keyValuePair.Key)
                    {
                        SpriteReplacer spriteReplacer = SpriteReplacer.Attach(
                            spriteRenderer,
                            Skin.BodySprites
                        );
                        spriteReplacers.Add(spriteReplacer);
                    }
                }
            }
            if (wasWorking)
                RepStart();
        }

        public void RepStart()
        {
            foreach (SpriteReplacer spriteReplacer in spriteReplacers)
            {
                spriteReplacer.Apply();
            }
            Working = true;
        }

        public void RepEnd()
        {
            foreach (SpriteReplacer spriteReplacer in spriteReplacers)
            {
                if (spriteReplacer != null)
                    spriteReplacer.Restore();
            }
            Working = false;
        }

        public void ResetSkin()
        {
            RepEnd();
            Skin = null;
            foreach (SpriteReplacer replacer in spriteReplacers)
            {
                replacer.Restore();
                Destroy(replacer);
            }
            spriteReplacers.Clear();
        }

        private void OnDestroy() => RepEnd();

        public bool Working { get; private set; } = false;
        public uint OwnerID { get; private set; }
        public SkinObject Skin { get; private set; }
        public NetBody NBody { get; private set; }
        List<SpriteRenderer> spriteRenderers = new();
        List<SpriteReplacer> spriteReplacers = new();

        internal static readonly string[] names =
        {
            "experimentTail",
            "experimentFoot",
            "experimentUpTorso",
            "experimentUpArm",
            "experimentThigh",
            "experimentDownTorso",
            "experimentDownArm",
            "experimentCrus",
            "experimentEyeGoneHealed",
            "experimentEyeGone",
            "experimentEyeClosed",
            "experimentEyeScaredBack",
            "experimentEyeScared",
            "experimentEyeSadBack",
            "experimentEyeSad",
            "experimentHead",
            "experimentEyePanic",
            "experimentEyeOpen",
            "experimentEyeLookBack",
            "experimentEyeHalfClosedBack",
            "experimentEyeHalfClosed",
            "experimentHeadDisfigured3Healed",
            "experimentHeadDisfigured3",
            "experimentHeadDisfigured2Healed",
            "experimentHeadDisfigured2",
            "experimentHeadDisfigured1Healed",
            "experimentHeadDisfigured1",
            "experimentHeadBackMouth",
            "experimentHeadBackMouthMini",
            "experimentHeadBack",
            "experimentHandB",
            "experimentHandF",
            "experimentNosebleed",
            "experimentEyeHappy",
        };
    }
}
