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
                SpriteRenderer spriteRenderer in gameObject.GetComponentsInChildren<SpriteRenderer>()
            )
            {
                foreach (string name in names)
                {
                    if (spriteRenderer.name == name)
                        spriteRenderers.Add(spriteRenderer);
                }
            }
        }

        public void ApplySkin(SkinObject _skin)
        {
            bool wasWorking = Working;
            if (wasWorking)
                RepEnd();
            Skin = _skin;
            foreach (KeyValuePair<string, Sprite> keyValuePair in Skin.BodySprites)
            {
                foreach (SpriteRenderer spriteRenderer in spriteRenderers)
                {
                    if (spriteRenderer.name == keyValuePair.Key)
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
                spriteReplacer.Restore();
            }
            spriteReplacers.Clear();
            Working = false;
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
