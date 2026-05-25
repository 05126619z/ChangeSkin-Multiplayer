using UnityEngine;

namespace ChangeSkinMP;

public class RemoteSkinController : MonoBehaviour
{
    public ChangeBody CBody { get; private set; }
    public bool Banned { get; private set; }

    private void Awake()
    {
        CBody = GetComponent<ChangeBody>();
    }

    public void SetSkin(SkinObject skin)
    {
        CBody.ApplySkin(skin);
        if (!Banned)
            CBody.RepStart();
    }

    public void RequestSkin() { }

    public void Enable()
    {
        if (Banned)
            return;
        CBody.RepStart();
    }

    public void Disable()
    {
        CBody.RepEnd();
    }

    public void OnBanReceived(bool banned)
    {
        Banned = banned;
        if (banned)
            CBody.ResetSkin();
        else if (CBody.Skin != null)
            CBody.RepStart();
    }
}
